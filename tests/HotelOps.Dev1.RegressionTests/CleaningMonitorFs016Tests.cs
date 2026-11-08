using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MonitorFs016SqlAttribute : FactAttribute
{
    public MonitorFs016SqlAttribute()
    { if (Environment.GetEnvironmentVariable("FS016_MONITOR_SQL") != "READ_ONLY_FS015") Skip = "Opt-in read-only monitor check against existing synthetic isolated FS015 trial."; }
}

[Collection("HotelOps isolated SQL")]
public sealed class CleaningMonitorFs016Tests
{
    [Fact]
    public async Task Monitor_rejects_mismatched_or_restricted_identity_before_SQL()
    {
        var service = new CleaningMonitorService(null!, new Identity(9), null!, null!, TimeProvider.System);
        var ticket = CleaningClosureWorkflowTests.Ticket(8);
        Assert.Equal(CleaningMonitorStatus.SessionInvalid, (await service.ListAsync(ticket, 1, 1, new(), default)).Status);
    }

    [MonitorFs016Sql]
    public async Task Monitor_reads_current_cycle_and_unresolved_blockers_without_writing_source()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(120)); var ct = budget.Token;
        // This legacy read-only check remains pinned to the old trial; FS016 rework Monitor assertions run in ReworkWorkflowSqlTests.
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(CleaningClosureWorkflowTests.Connection.Replace("Connect Timeout=5", "Connect Timeout=30")).Options);
        Assert.Equal("HotelOps_Canonical_FS015_Cleaning_V1_Fresh", db.Database.GetDbConnection().Database);
        var supervisor = await db.Users.AsNoTracking().SingleAsync(x => x.Username == "fs015.supervisor", ct);
        var branch = supervisor.PrimaryBranchId!.Value;
        var identity = new Identity(supervisor.UserId);
        var permissions = new PermissionService(identity, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System);
        var service = new CleaningMonitorService(db, identity, permissions, new Sessions(supervisor.UserId), TimeProvider.System);
        var ticket = CleaningClosureWorkflowTests.Ticket(supervisor.UserId);
        var task = await db.CleaningTasks.AsNoTracking().Where(x => x.Room.BranchId == branch).OrderByDescending(x => x.CleaningTaskId).FirstAsync(ct);
        var before = await Snapshot(db, task.CleaningTaskId, ct);
        var result = await service.DetailAsync(ticket, 1, branch, "cleaning", task.CleaningTaskId, ct);
        Assert.Equal(CleaningMonitorStatus.Success, result.Status);
        var item = Assert.IsType<CleaningMonitorDetail>(result.Value).Item;
        var current = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.room_cleaning_cycle_state WHERE room_id={task.RoomId} AND cleaning_task_id={task.CleaningTaskId}").SingleAsync(ct);
        Assert.Equal(current == 1, item.IsCurrentCycle);
        var unresolved = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.cleaning_progress_logs p
            LEFT JOIN dbo.fs014_cleaning_blocker_resolutions b ON b.progress_id=p.progress_id
            WHERE p.cleaning_task_id={task.CleaningTaskId} AND p.status='BLOCKED'
              AND (b.progress_id IS NULL OR b.resolved_at IS NULL OR b.incident_id IS NOT NULL OR b.material_request_id IS NOT NULL)
            """).SingleAsync(ct);
        Assert.Equal(unresolved > 0, item.HasUnresolvedBlocker);
        if (unresolved == 0) { Assert.Null(item.BlockerCode); Assert.Null(item.BlockerNote); }
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(task.CreatedAt, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
        var page = await service.ListAsync(ticket, 1, branch, new(FromDate: day, PageSize: 1), ct);
        Assert.Equal(CleaningMonitorStatus.Success, page.Status);
        Assert.InRange(Assert.IsType<CleaningMonitorPage>(page.Value).Items.Count, 0, 1);
        if (item.WorkflowState != null && item.WorkflowState != item.RecordedStatus)
        {
            var legacyFilter = await service.ListAsync(ticket, 1, branch,
                new(FromDate: day, Status: item.RecordedStatus, Search: task.TaskNo), ct);
            Assert.Equal(CleaningMonitorStatus.Success, legacyFilter.Status);
            Assert.DoesNotContain(Assert.IsType<CleaningMonitorPage>(legacyFilter.Value).Items, x => x.Kind == "cleaning" && x.Id == task.CleaningTaskId);
        }
        Assert.Equal(before, await Snapshot(db, task.CleaningTaskId, ct));
        // An explicit branch grant does not authorize reading this source through a different branch.
        var wrongBranch = await db.Branches.Where(x => x.BranchId != branch).Select(x => x.BranchId).FirstAsync(ct);
        Assert.Equal(CleaningMonitorStatus.Forbidden, (await service.DetailAsync(ticket, 1, wrongBranch, "cleaning", task.CleaningTaskId, ct)).Status);
    }

    private static Task<string> Snapshot(HotelOpsDbContext db, long task, CancellationToken ct) => db.Database.SqlQuery<string>($"""
        SELECT (SELECT (SELECT * FROM dbo.cleaning_tasks WHERE cleaning_task_id={task} FOR JSON PATH) AS source,
          (SELECT COUNT(*) FROM dbo.audit_logs) AS audits,(SELECT COUNT(*) FROM dbo.notification_publications) AS publications,
          (SELECT COUNT(*) FROM dbo.work_intervals) AS intervals,(SELECT COUNT(*) FROM dbo.room_readiness_events) AS readiness
        FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [Value]
        """).SingleAsync(ct);

    private sealed class Identity(long actor) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(actor, 1, false); return true; } }
    private sealed class Sessions(long actor) : ISqlSessionService
    {
        public Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default)
            => Task.FromResult(new SessionValidationResult(true, new(1, actor, ticket.CreatedAtUtc, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), false)));
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult credential, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
