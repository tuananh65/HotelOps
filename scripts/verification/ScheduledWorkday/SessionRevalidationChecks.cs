using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Opt-in real SQL regression. All fixture/session/workday/audit writes roll back.
// The cookie identity is simulated; session validation and permissions use production SQL services.
internal static class SessionRevalidationChecks
{
    public static async Task CheckCurrentEndAsync(string username)
    {
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_RECOVERY_CONNECTION") ?? throw new Exception("Missing connection");
        var rollback = new RollbackCommit();
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(rollback).Options);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Username == username);
        var day = await db.Workdays.AsNoTracking().SingleOrDefaultAsync(d => d.UserId == user.UserId && d.Status == "WORKING");
        if (day == null) { Console.WriteLine("NO_WORKING_DAY"); return; }
        Console.WriteLine($"Current workday={day.WorkdayId}, status={day.Status}");
        var created = DateTime.UtcNow;
        var ticket = SessionReference.FromProtectedTicket(user.UserId, SessionCredential.Generate(), false, created);
        var actor = new ProbeActor(user.UserId);
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.Request.Scheme = "https";
        http.HttpContext.User = HotelOpsCookieTicket.Create(new CreatedSqlSession(ticket, new SessionSnapshot(1,user.UserId,created,created.AddMinutes(10),created.AddHours(1),false)),TimeProvider.System).Principal;
        var sessions = new FixtureSession(db,"current-end",day.BranchId,day.ShiftAssignmentId ?? 0);
        var permissions = new PermissionService(actor,new EfPermissionDataSource(db),new WorkdayPermissionResourceResolver(db,null!),TimeProvider.System);
        var service = new WorkdayService(db,actor,http,sessions,permissions,new SqlWorkTimeTransactionParticipant(db),new OwnWorkdaySnapshotReader(db,actor));
        var result = await service.EndAsync(day.BranchId,day.WorkdayId,day.RowVersion,false,default);
        if (result.Status is not (200 or 409) || (result.Status == 200) != rollback.Intercepted)
            throw new Exception($"Unexpected end outcome: {result.Status} {result.Code}");
        Console.WriteLine($"END_RESULT={result.Status} {result.Code}; commit rolled back={rollback.Intercepted}");
        foreach(var task in await db.CleaningTasks.AsNoTracking().Where(t=>t.Status=="IN_PROGRESS" && t.CleaningAssignments.Any(a=>a.AssigneeUserId==user.UserId && a.AssignmentStatus=="ACTIVE" && a.EndedAt==null)).Select(t=>new{t.CleaningTaskId,t.Room.RoomNumber,t.Status}).ToArrayAsync())
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(task));
        Console.WriteLine("OPEN_INTERVALS="+await db.WorkIntervals.CountAsync(i=>i.UserId==user.UserId && i.ClosedAt==null));
        Console.WriteLine("PERSISTED_STATUS="+await db.Workdays.Where(d=>d.WorkdayId==day.WorkdayId).Select(d=>d.Status).SingleAsync());
        if (!await db.Workdays.AnyAsync(d=>d.WorkdayId==day.WorkdayId && d.Status=="WORKING" && d.EndedAt==null))
            throw new Exception("Probe changed the real workday.");
    }

    public static async Task RunAsync(string username)
    {
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_RECOVERY_CONNECTION")
            ?? throw new InvalidOperationException("Set HOTELOPS_RECOVERY_CONNECTION locally.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = timeout.Token;
        await using var baseline = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
        var user = await baseline.Users.AsNoTracking().SingleAsync(u => u.Username == username, ct);
        var branch = user.PrimaryBranchId ?? throw new Exception("No primary branch.");
        if (user.TemporaryPasswordExpiresAt != null || await baseline.Workdays.AnyAsync(d => d.UserId == user.UserId && d.Status == "WORKING", ct))
            throw new Exception("Use an unrestricted employee with no working day and an assigned shift today.");
        var now = await baseline.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(now, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
        var assignment = await ScheduledWorkdayClaims.Unclaimed(baseline.ShiftAssignments.AsNoTracking(), baseline.Workdays.AsNoTracking())
            .Where(a => a.UserId == user.UserId && a.Shift.BranchId == branch && a.Shift.ShiftDate == today && a.AssignmentStatus == "ASSIGNED")
            .Select(a => a.ShiftAssignmentId).FirstAsync(ct);
        var daysBefore = await baseline.Workdays.CountAsync(d => d.UserId == user.UserId, ct);
        var auditsBefore = await baseline.AuditLogs.CountAsync(a => a.ActorUserId == user.UserId && a.EntityType == "workdays", ct);
        var sessionsBefore = await baseline.UserSessions.CountAsync(s => s.UserId == user.UserId, ct);

        foreach (var scenario in new[] { "start", "end", "stale-end", "duplicate-start", "revoked-before-commit" })
        {
            var rollback = new RollbackCommit();
            await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(rollback).Options);
            var created = DateTime.UtcNow;
            var ticket = SessionReference.FromProtectedTicket(user.UserId, SessionCredential.Generate(), false, created);
            var actor = new ProbeActor(user.UserId);
            var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            http.HttpContext.Request.Scheme = "https";
            http.HttpContext.User = HotelOpsCookieTicket.Create(new CreatedSqlSession(ticket,
                new SessionSnapshot(1, user.UserId, created, created.AddMinutes(10), created.AddHours(1), false)), TimeProvider.System).Principal;
            var sessions = new FixtureSession(db, scenario, branch, assignment);
            var permissions = new PermissionService(actor, new EfPermissionDataSource(db), new WorkdayPermissionResourceResolver(db, null!), TimeProvider.System);
            var service = new WorkdayService(db, actor, http, sessions, permissions, new SqlWorkTimeTransactionParticipant(db), new OwnWorkdaySnapshotReader(db, actor));
            rollback.BeforeRollback = async () =>
            {
                var day = await db.Workdays.AsNoTracking().SingleAsync(d => d.UserId == user.UserId && d.ShiftAssignmentId == assignment, ct);
                var expected = scenario == "end" ? "ENDED" : "WORKING";
                if (day.Status != expected || day.RowVersion.Length != 8) throw new Exception("Incorrect persisted workday receipt.");
                var auditCount = await db.AuditLogs.CountAsync(a => a.ActorUserId == user.UserId && a.EntityType == "workdays" && a.EntityId == day.WorkdayId.ToString(), ct);
                if (auditCount != (scenario == "end" ? 2 : 1)) throw new Exception("Missing transactional workday audit.");
            };
            WorkdayResult result;
            if (scenario is "end" or "stale-end")
            {
                // Fixture is installed in the service-owned transaction at first session validation.
                // Invoke the same Execute boundary with an action that reads that fresh fixture.
                var execute = typeof(WorkdayService).GetMethod("Execute", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                Func<CurrentUserInfo, Task<WorkdayResult>> action = async _ =>
                {
                    var seed = sessions.SeededDay!;
                    var participant = new SqlWorkTimeTransactionParticipant(db);
                    await participant.LockUsersAsync([user.UserId], ct);
                    var day = await participant.EndWorkdayAsync(user.UserId, branch, seed.WorkdayId,
                        scenario == "stale-end" ? new byte[8] : seed.RowVersion, ct);
                    return new WorkdayResult(200, day.Status);
                };
                result = await (Task<WorkdayResult>)execute.Invoke(service, [branch, "ATTENDANCE_RECORD", action, ct, null, null])!;
            }
            else result = await service.StartAsync(branch, new StartWorkdayRequest { ShiftAssignmentId = assignment }, ct);
            var expectedStatus = scenario switch { "start" => 201, "end" => 200, "stale-end" => 412, "duplicate-start" => 409, _ => 401 };
            if (result.Status != expectedStatus) throw new Exception($"{scenario}: expected {expectedStatus}, got {result.Status} {result.Code}");
            if (scenario == "start" && (result.Value is not WorkdayResponse receipt || receipt.Status != "WORKING" || Convert.FromBase64String(receipt.RowVersion).Length != 8))
                throw new Exception("Invalid start response.");
            if (rollback.Intercepted != (expectedStatus < 300)) throw new Exception("Unexpected commit boundary.");
            if (await baseline.Workdays.CountAsync(d => d.UserId == user.UserId, ct) != daysBefore
                || await baseline.AuditLogs.CountAsync(a => a.ActorUserId == user.UserId && a.EntityType == "workdays", ct) != auditsBefore
                || await baseline.UserSessions.CountAsync(s => s.UserId == user.UserId, ct) != sessionsBefore)
                throw new Exception("Rollback did not restore baseline.");
            Console.WriteLine($"PASS {scenario}: {result.Status}; real SQL session validation; writes rolled back.");
        }
    }

    private sealed class ProbeActor(long userId) : ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(userId, 1, false); return true; }
    }

    private sealed class RollbackCommit : DbTransactionInterceptor
    {
        public Func<Task>? BeforeRollback;
        public bool Intercepted;
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (BeforeRollback != null) await BeforeRollback();
            await transaction.RollbackAsync(cancellationToken);
            Intercepted = true;
            return InterceptionResult.Suppress();
        }
    }

    private sealed class FixtureSession(HotelOpsDbContext db, string scenario, long branch, long assignment) : ISqlSessionService
    {
        private int calls;
        public Workday? SeededDay;
        public async Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken ct = default)
        {
            calls++;
            if (calls == 1)
            {
                var created = ticket.CreatedAtUtc;
                db.UserSessions.Add(new UserSession { UserId = ticket.UserId, SessionKeyHash = SessionCredential.Hash(ticket.ExportCredentialForProtectedTicket()), CreatedAt = created, LastUsedAt = created, ExpiresAt = created.AddMinutes(10) });
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                if (scenario is "end" or "stale-end" or "duplicate-start")
                {
                    var participant = new SqlWorkTimeTransactionParticipant(db);
                    await participant.LockUsersAsync([ticket.UserId], ct);
                    SeededDay = await participant.StartScheduledWorkdayAsync(ticket.UserId, branch, assignment, ct);
                    db.ChangeTracker.Clear();
                }
            }
            var keyHash = SessionCredential.Hash(ticket.ExportCredentialForProtectedTicket());
            if (calls == 2 && scenario == "revoked-before-commit")
                await db.UserSessions.Where(s => s.SessionKeyHash == keyHash).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
            var real = new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System);
            var result = await real.ValidateSessionAsync(ticket, ct);
            // Normalize only the fixture's generated ID to the simulated cookie identity.
            return result.Session == null ? result : result with { Session = result.Session with { SessionId = 1 } };
        }
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
