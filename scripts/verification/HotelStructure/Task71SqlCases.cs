using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
internal static class Task71SqlCases
{
    internal static async Task RunAsync(HotelOpsDbContext setup, Func<DbCommandInterceptor?, HotelOpsDbContext> open,
        long actor, long manager, long branch, long unusedType, long replacement, long room, long floor, long department,
        Action<bool, string> check, CancellationToken ct)
    {
        var raw = SessionCredential.Generate(); var now = DateTime.UtcNow;
        var session = new UserSession { UserId = actor, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(30) };
        setup.Add(session); await setup.SaveChangesAsync(ct); setup.ChangeTracker.Clear();
        var ticket = SessionReference.FromProtectedTicket(actor, raw, false, now);
        var snapshot = new SessionSnapshot(session.SessionId, actor, now, session.ExpiresAt, now.AddHours(12), false);
        RoomTypeCreationService Service(HotelOpsDbContext db)
        {
            var context = new DefaultHttpContext { User = HotelOpsCookieTicket.Create(new(ticket, snapshot), TimeProvider.System).Principal };
            context.Items[typeof(SessionSnapshot)] = snapshot; var current = new CurrentUserAccessor(new Context { HttpContext = context });
            return new(db, current, new PermissionService(current, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System),
                new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System), TimeProvider.System);
        }
        Task<string> State() => setup.Database.SqlQueryRaw<string>("SELECT (SELECT * FROM dbo.room_types ORDER BY room_type_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.rooms ORDER BY room_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.departments ORDER BY department_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.audit_logs ORDER BY audit_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.room_status_history ORDER BY history_id FOR JSON PATH,INCLUDE_NULL_VALUES)+(SELECT * FROM dbo.user_roles ORDER BY user_role_id FOR JSON PATH,INCLUDE_NULL_VALUES) AS [Value]").SingleAsync(ct);
        foreach (var uc in new[] { "UC019", "UC021", "UC024", "UC025" })
        foreach (var fault in new[] { "session", "grant", "cancel", "manager", "membership", "manager-user" }.Where(x => x is "session" or "grant" or "cancel" || uc is "UC024" or "UC025"))
        {
            string? tag = null; await using (var reader = open(null))
            {
                var s = Service(reader);
                if (uc == "UC019") tag = (await s.ReadRoomTypeAsync(ticket, session.SessionId, branch, unusedType, ct)).ETag;
                if (uc == "UC021") tag = (await s.ReadEditableRoomAsync(ticket, session.SessionId, branch, room, ct)).ETag;
                if (uc == "UC025") tag = (await s.ReadDepartmentAsync(ticket, session.SessionId, branch, department, ct)).ETag;
            }
            var before = await State(); using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var hook = new Probe { Uc = uc, Fault = fault, Actor = actor, Manager = manager, Session = session.SessionId, Type = unusedType, Stop = stop };
            await using var writer = open(hook); RoomTypeBatchResult? result = null; var cancelled = false;
            try
            {
                var service = Service(writer); var code = "T71_SQL_" + fault;
                result = uc switch
                {
                    "UC019" => await service.DeactivateRoomTypeAsync(ticket, session.SessionId, branch, unusedType, tag!, new(true, "SQL final-boundary retirement"), stop.Token),
                    "UC021" => await service.UpdateRoomInformationAsync(ticket, session.SessionId, branch, room, tag!, new(floor, replacement, code, "SQL final-boundary room update"), stop.Token),
                    "UC024" => await service.CreateDepartmentAsync(ticket, session.SessionId, branch, new(code, code, ManagerUserId: manager), stop.Token),
                    _ => await service.UpdateDepartmentAsync(ticket, session.SessionId, branch, department, tag!, new(code, ManagerUserId: manager), stop.Token)
                };
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { cancelled = true; }
            check(hook.Fired && hook.SawBusiness, uc + " SQL proves business write before final " + fault);
            check(fault == "cancel" ? cancelled : result?.Status == (fault == "session" ? RoomTypeBatchStatus.SessionInvalid : fault == "grant" ? RoomTypeBatchStatus.Forbidden : RoomTypeBatchStatus.InvalidReference), uc + " rejects final " + fault);
            check(await State() == before && writer.Database.CurrentTransaction == null && !writer.ChangeTracker.Entries().Any(), uc + " " + fault + " rolls back all business/audit/RBAC/history and cleans context");
        }
        // A real competing manager writer wins before the operation acquires User locks.
        await using var holder = open(null); await holder.Database.OpenConnectionAsync(ct);
        var owner = await holder.Database.SqlQueryRaw<int>("SELECT CONVERT(int,@@SPID) AS [Value]").SingleAsync(ct);
        await using var tx = await holder.Database.BeginTransactionAsync(ct);
        await new UserEligibilityLock(holder).LockAsync([manager], ct);
        await holder.UserRoles.Where(x => x.UserId == manager).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), ct);
        await using var contender = open(null);
        var pending = Service(contender).CreateDepartmentAsync(ticket, session.SessionId, branch, new("T71_MANAGER_RACE", "Manager race", ManagerUserId: manager), ct);
        var waited = false;
        try
        {
            for (var i = 0; i < 160 && !pending.IsCompleted; i++)
            {
                waited = await setup.Database.SqlQuery<int>($"SELECT CONVERT(int,COUNT(*)) AS [Value] FROM sys.dm_exec_requests WHERE blocking_session_id={owner} AND wait_type LIKE 'LCK_M_%'").SingleAsync(ct) > 0;
                if (waited) break; await Task.Delay(30, ct);
            }
            check(waited, "UC024 actual DMV wait on competing manager User lock");
            await tx.CommitAsync(ct);
            check((await pending).Status == RoomTypeBatchStatus.InvalidReference && !await setup.Departments.AnyAsync(x => x.DepartmentCode == "T71_MANAGER_RACE", ct), "UC024 rereads committed manager revocation, no stale designation");
        }
        finally
        {
            if (holder.Database.CurrentTransaction != null) await tx.RollbackAsync(CancellationToken.None);
            try { await pending; } catch { }
            await setup.UserRoles.Where(x => x.UserId == manager).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true), CancellationToken.None);
        }
    }
    private sealed class Context : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
    private sealed class Probe : DbCommandInterceptor
    {
        internal string Uc = "", Fault = ""; internal long Actor, Manager, Session, Type;
        internal CancellationTokenSource Stop = null!; internal bool Fired, SawBusiness;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (Fired || !command.CommandText.Contains("INSERT INTO [audit_logs]")) return result;
            Fired = true; await using var proof = command.Connection!.CreateCommand(); proof.Transaction = command.Transaction;
            proof.CommandText = Uc switch { "UC019" => "SELECT COUNT(*) FROM dbo.room_types WHERE room_type_id=@type AND is_active=0", "UC021" => "SELECT COUNT(*) FROM dbo.rooms WHERE room_number=@code", "UC024" => "SELECT COUNT(*) FROM dbo.departments WHERE department_code=@code", _ => "SELECT COUNT(*) FROM dbo.departments WHERE department_name=@code" };
            proof.Parameters.Add(new SqlParameter("@type", Type)); proof.Parameters.Add(new SqlParameter("@code", "T71_SQL_" + Fault));
            SawBusiness = (int)(await proof.ExecuteScalarAsync(ct))! == 1;
            if (Fault == "cancel") { Stop.Cancel(); Stop.Token.ThrowIfCancellationRequested(); }
            proof.Parameters.Clear(); proof.CommandText = Fault switch
            {
                "session" => "UPDATE dbo.user_sessions SET expires_at=DATEADD(second,-1,SYSUTCDATETIME()) WHERE session_id=@id",
                "membership" => "UPDATE dbo.user_branch_access SET revoked_at=SYSUTCDATETIME() WHERE user_id=@id",
                "manager-user" => "UPDATE dbo.users SET account_status='INACTIVE' WHERE user_id=@id",
                _ => "UPDATE dbo.user_roles SET expires_at=DATEADD(second,-1,SYSUTCDATETIME()) WHERE user_id=@id"
            };
            proof.Parameters.Add(new SqlParameter("@id", Fault == "session" ? Session : Fault == "grant" ? Actor : Manager)); await proof.ExecuteNonQueryAsync(ct); return result;
        }
    }
}
