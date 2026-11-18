#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.WorkTime;
using HotelOps.Api.DTOs.OperationalTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Opt-in local SQL check. No password is read or reset. All write scenarios roll back.
// The principal ID is simulated; session validation and permission checks use real SQL.
internal static class StaffWorkspaceChecks
{
    private sealed class Actor(long id) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(id, 1, false); return true; } }
    private sealed class Rollback : DbTransactionInterceptor
    {
        public Func<Task>? Inspect;
        public bool Intercepted;
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data, InterceptionResult result, CancellationToken ct = default)
        { if (Inspect != null) await Inspect(); await transaction.RollbackAsync(ct); Intercepted = true; return InterceptionResult.Suppress(); }
    }
    private sealed class Session(HotelOpsDbContext db) : ISqlSessionService
    {
        private bool seeded;
        public async Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken ct = default)
        {
            if (!seeded)
            {
                if (db.Database.CurrentTransaction == null) throw new Exception("Fixture requires transaction.");
                db.UserSessions.Add(new UserSession { UserId=ticket.UserId, SessionKeyHash=SessionCredential.Hash(ticket.ExportCredentialForProtectedTicket()), CreatedAt=ticket.CreatedAtUtc, LastUsedAt=ticket.CreatedAtUtc, ExpiresAt=ticket.CreatedAtUtc.AddMinutes(10) });
                await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); seeded=true;
            }
            var real = await new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System).ValidateSessionAsync(ticket, ct);
            return real.Session == null ? real : real with { Session = real.Session with { SessionId = 1 } };
        }
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult c,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference t,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference t,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long id,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    public static async Task Main()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_RECOVERY_CONNECTION") ?? throw new Exception("Missing configuration.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        await using var baseline=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var user=await baseline.Users.AsNoTracking().SingleAsync(u=>u.Username=="lan.dang",ct);
        var branch=user.PrimaryBranchId ?? throw new Exception("Missing primary branch.");
        var requestsBefore=await baseline.InternalRequests.CountAsync(ct);var sessionsBefore=await baseline.UserSessions.CountAsync(ct);var auditsBefore=await baseline.AuditLogs.CountAsync(ct);
        InternalRequestIntakeOptions? options=null;
        foreach(var scenario in new[]{"options","task-branches","tasks","history","create","material-denied","foreign-branch-denied"})
        {
            var rollback=new Rollback();
            await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(rollback).Options);
            var actor=new Actor(user.UserId);var session=new Session(db);
            var ticket=SessionReference.FromProtectedTicket(user.UserId,SessionCredential.Generate(),false,DateTime.UtcNow);
            var intake=new InternalRequestIntakeService(db,session,actor);
            if(scenario=="options")
            {
                options=await intake.OptionsAsync(ticket,1,branch,ct);
                if(options.Branches.Length!=1 || options.Branches[0].Id!=branch || options.Departments.Length==0 || options.Types.Length==0)throw new Exception("Wrong intake scope/options.");
                Console.WriteLine($"PASS options: {options.Types.Length} types, {options.Departments.Length} departments; branch={branch}");
            }
            else if(scenario=="task-branches" || scenario=="tasks")
            {
                var permission=new PermissionService(actor,new EfPermissionDataSource(db),new WorkdayPermissionResourceResolver(db,null!),TimeProvider.System);
                var service=new RoomTypeCreationService(db,actor,permission,session,TimeProvider.System);
                var result=scenario=="task-branches" ? await service.OperationalTaskBranchesAsync(ticket,1,ct) : await service.ReadOperationalTasksAsync(ticket,1,branch,new OperationalTaskQuery(),null,ct);
                if(result.Status!=RoomTypeBatchStatus.Success)throw new Exception($"{scenario}: {result.Status}");
                Console.WriteLine("PASS "+scenario+": "+System.Text.Json.JsonSerializer.Serialize(result));
            }
            else if(scenario=="history")
            {
                var history=await new InternalRequestReadService(db,new InternalRequestReadAccess(db),session,actor,TimeProvider.System).ListAsync(ticket,1,null,new InternalRequestHistoryFilter(),ct);
                if(history.Items.Any(r=>string.IsNullOrWhiteSpace(r.BranchName) || r.ReceivingDepartmentId!=null && string.IsNullOrWhiteSpace(r.ReceivingDepartmentName)))throw new Exception("Missing scoped display names.");
                Console.WriteLine($"PASS history: {history.TotalCount} records; names resolved.");
            }
            else
            {
                var type=options!.Types.First(t=>t.MaterialNeed==(scenario=="material-denied"));
                var input=new InternalRequestIntakeInput(Guid.NewGuid(),scenario=="foreign-branch-denied"?long.MaxValue:branch,type.Id,options.Departments.First().Id,"Verification rolled back","Verify intake authority, room, audit and outbox.",options.Priorities.First().Id,DateTimeOffset.UtcNow.AddDays(1),RoomId:options.Rooms.First().Id,RequesterDepartmentId:options.RequesterDepartments!.Single().Id);
                rollback.Inspect=async()=>
                {
                    var row=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.RequestNo=="IR-"+input.SubmissionId.ToString("N"),ct);
                    if(row.Status!="PENDING_ACCEPTANCE" || row.RequesterId!=user.UserId || row.RoomId!=input.RoomId)throw new Exception("Wrong persisted request.");
                    if(!await db.AuditLogs.AnyAsync(a=>a.EntityType=="internal_requests"&&a.EntityId==row.InternalRequestId.ToString()&&a.Action=="CREATE",ct))throw new Exception("Missing audit.");
                    var count=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.internal_request_intake_outbox o JOIN dbo.internal_request_events e ON e.event_id=o.event_id WHERE e.internal_request_id={row.InternalRequestId}").SingleAsync(ct);
                    if(count!=1)throw new Exception("Expected one authorized recipient.");
                };
                try
                {
                    var result=await intake.CreateAsync(ticket,1,input,System.IO.Path.GetTempPath(),ct);
                    if(scenario!="create"||result.Status!="PENDING_ACCEPTANCE"||!rollback.Intercepted)throw new Exception("Unexpected create outcome.");
                    Console.WriteLine("PASS create: request + room + event + audit + recipient outbox, rolled back.");
                }
                catch(InternalRequestReadException e) when(scenario=="material-denied"&&e.Status==400&&e.Code=="MATERIAL_REQUEST_REQUIRED" || scenario=="foreign-branch-denied"&&e.Status==403)
                { Console.WriteLine($"PASS {scenario}: {e.Status} {e.Code}"); }
            }
            if(await baseline.InternalRequests.CountAsync(ct)!=requestsBefore || await baseline.UserSessions.CountAsync(ct)!=sessionsBefore || await baseline.AuditLogs.CountAsync(ct)!=auditsBefore)throw new Exception("Rollback changed baseline counts.");
        }
        Console.WriteLine("PASS baseline unchanged; real SQL/session/permission checks; simulated principal.");
    }
}
