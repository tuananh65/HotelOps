using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class WorkspaceReadSqlAttribute : FactAttribute
{
    public WorkspaceReadSqlAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOTELOPS_WORKSPACE_READ_SQL")))
            Skip = "Opt-in read-only verification with existing account permissions; no fixtures are written.";
    }
}

[Collection("HotelOps isolated SQL")]
public sealed class WorkspaceReadSqlTests
{
    [WorkspaceReadSql]
    public async Task Rework_default_list_reads_historical_rows_and_date_filter_is_optional()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("HOTELOPS_WORKSPACE_READ_SQL")).Options);
        var account = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "mai.tran", ct);
        var branch = account.PrimaryBranchId!.Value;
        var identity = new Identity(account.UserId);
        var permission = new PermissionService(identity, new EfPermissionDataSource(db), new UnresolvedPermissionResourceResolver(), TimeProvider.System);
        var service = new CleaningMonitorService(db, identity, permission, new Sessions(account.UserId), TimeProvider.System);
        var ticket = CleaningClosureWorkflowTests.Ticket(account.UserId);
        var before = await db.AuditLogs.AsNoTracking().CountAsync(ct);
        var response = await service.ListAsync(ticket, 1, branch, new(Kind: "rework", PageSize: 100), ct);
        Assert.Equal(CleaningMonitorStatus.Success, response.Status);
        var all = Assert.IsType<CleaningMonitorPage>(response.Value);
        Assert.Null(all.FromDate); Assert.Null(all.ToDate);
        Assert.NotEmpty(all.Items);
        Assert.All(all.Items, item => Assert.Equal("rework", item.Kind));
        var id = all.Items[0].Id;
        var created = await db.CleaningReworks.AsNoTracking().Where(r => r.ReworkId == id).Select(r => r.RequestedAt).SingleAsync(ct);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(created, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
        var filtered = await service.ListAsync(ticket, 1, branch, new(FromDate: day, ToDate: day, Kind: "rework", PageSize: 100), ct);
        Assert.Equal(CleaningMonitorStatus.Success, filtered.Status);
        Assert.Contains(Assert.IsType<CleaningMonitorPage>(filtered.Value).Items, item => item.Id == id);
        var foreignBranch = await db.Branches.AsNoTracking().Where(b => b.BranchId != branch).Select(b => b.BranchId).FirstAsync(ct);
        Assert.Equal(CleaningMonitorStatus.Forbidden, (await service.ListAsync(ticket, 1, foreignBranch, new(Kind: "rework"), ct)).Status);
        Assert.Equal(before, await db.AuditLogs.AsNoTracking().CountAsync(ct));
    }

    private sealed class Identity(long actor) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(actor, 1, false); return true; } }
    // Only the test session boundary is substituted; real persisted permission paths are evaluated.
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
