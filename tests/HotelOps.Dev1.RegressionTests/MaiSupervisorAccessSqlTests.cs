using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

[Collection("Transfer access database")]
public sealed class MaiSupervisorAccessSqlTests
{
    private static HotelOpsDbContext Context()
    {
        var connection = Environment.GetEnvironmentVariable("TRANSFER_REQUEST_TEST_CONNECTION")!;
        Assert.Matches("^HotelOps_TransferTest_[a-f0-9]{32}$", new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog);
        return new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
    }
    private sealed class Actor(long id, long sid) : ICurrentUserAccessor
    { public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(id, sid, false); return true; } }
    private static async Task<(User User, SessionReference Ticket, long Sid, Actor Actor, PermissionService Permissions, RoomTypeCreationService Service)> Session(HotelOpsDbContext db)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "mai.tran");
        var now = DateTime.UtcNow; var raw = SessionCredential.Generate();
        var session = new UserSession { UserId = user.UserId, SessionKeyHash = SessionCredential.Hash(raw), CreatedAt = now, LastUsedAt = now, ExpiresAt = now.AddMinutes(20) };
        db.UserSessions.Add(session); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var actor = new Actor(user.UserId, session.SessionId);
        var permissions = new PermissionService(actor, new EfPermissionDataSource(db),
            new HotelOps.Api.Services.WorkTime.WorkdayPermissionResourceResolver(db, null!), TimeProvider.System);
        var sessions = new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System);
        return (user, SessionReference.FromProtectedTicket(user.UserId, raw, false, now), session.SessionId, actor, permissions,
            new RoomTypeCreationService(db, actor, permissions, sessions, TimeProvider.System));
    }

    [HousekeepingWriterSqlFact]
    public async Task ApprovedOwnRequestGrantsEnableCreationAndMenuWithoutManagementOrCrossBranchAccess()
    {
        await using var db = Context(); var s = await Session(db); var branch = s.User.PrimaryBranchId!.Value;
        var access = new InsightAccess(db, s.Permissions, s.Actor);
        var nav = new HomeNavigationService(db, s.Actor, s.Permissions, TimeProvider.System,
            new AuditAccess(db, access, TimeProvider.System), new AssetMaintenanceHistoryReader(db, access, null!),
            new AssetLocationReview(db, access, null!), new AssetProfileAccess(db, access, null!));
        var result = await nav.ReadAsync(s.Ticket, default);
        // Navigation result is guarded by the real SQL session, not a mocked permission source.
        Assert.Equal(HotelOps.Api.Services.Users.UserDirectoryReadStatus.Success, result.Status);
        var links = Assert.IsType<string[]>(result.Value);
        foreach (var path in new[] { "/inspections/assignment", "/inspection-history", "/cleaning/assignment", "/internal-requests", "/internal-requests/new", "/material-requests/new", "/operations/material-requests", "/workday", "/workday/scheduled", "/operational-tasks" }) Assert.Contains(path, links);
        foreach (var path in new[] { "/users", "/roles", "/dashboard", "/inventory/stock", "/inventory/transfer-requests", "/operations/maintenance", "/operations/incidents", "/inspections/checklists", "/workday/shift-plans" }) Assert.DoesNotContain(path, links);
        foreach (var code in new[] { "MR_VIEW", "IR_VIEW" })
        {
            var grants = await new EfPermissionDataSource(db).ReadPathsAsync(s.User.UserId, code, DateTime.UtcNow, default);
            Assert.All(grants, g => { Assert.Equal("OWN", g.DataScope); Assert.Equal(branch, g.BranchId); });
            Assert.NotEmpty(grants);
            Assert.False((await s.Permissions.EvaluateBranchAsync(code, branch, default)).IsGranted);
        }
        var creation = await MaterialCreationAccess.BranchesAsync(db, s.User.UserId, DateTime.UtcNow, default);
        Assert.Equal(branch, Assert.Single(creation).BranchId);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var approvers = await MaterialApprovalAuthority.CandidatesAsync(db, s.User.UserId, branch, null, default);
            Assert.NotEmpty(approvers.Items); Assert.DoesNotContain(approvers.Items, p => p.UserId == s.User.UserId);
            await tx.RollbackAsync();
        }
        var materialScope = await MaterialRequestReadAccess.ReadAsync(db, s.User.UserId, branch, DateTime.UtcNow, default);
        Assert.True(materialScope.Own); Assert.False(materialScope.Branch);
        Assert.All(await materialScope.Apply(db.MaterialRequests).ToArrayAsync(), r => Assert.Equal(s.User.UserId, r.RequesterId));
        var requests = await new InternalRequestReadAccess(db).ReadAsync(s.User.UserId, DateTime.UtcNow, default);
        Assert.False(requests.Global); Assert.Empty(requests.Branches); Assert.Equal(new[] { branch }, requests.RelatedBranches);
        var other = await db.Branches.Where(b => b.IsActive && b.BranchId != branch).Select(b => b.BranchId).FirstAsync();
        Assert.False((await s.Permissions.EvaluateBranchAsync("ROOM_VIEW", other, default)).IsGranted);
        Assert.False((await MaterialRequestReadAccess.ReadAsync(db, s.User.UserId, other, DateTime.UtcNow, default)).Own);
    }

    [HousekeepingWriterSqlFact]
    public async Task HousekeepingAndInspectionReadsWorkButOtherBranchAndOtherInspectorAreDenied()
    {
        await using var db = Context(); var s = await Session(db); var branch = s.User.PrimaryBranchId!.Value;
        var options = await s.Service.InspectionHistoryBranchesAsync(s.Ticket, s.Sid, default, assignment: true);
        Assert.Equal(RoomTypeBatchStatus.Success, options.Status);
        Assert.Equal(branch, Assert.Single(Assert.IsAssignableFrom<IEnumerable<InspectionHistoryBranchDto>>(options.Value)).Id);
        Assert.Equal(RoomTypeBatchStatus.Success, (await s.Service.ReadInitialInspectionQueueAsync(s.Ticket, s.Sid, branch, null, 1, 25, default)).Status);
        Assert.Equal(RoomTypeBatchStatus.Success, (await s.Service.InspectionHistoryAsync(s.Ticket, s.Sid, new OperationalHistoryQuery(BranchId: branch), null, default)).Status);
        var monitor = new CleaningMonitorService(db, s.Actor, s.Permissions, new SqlSessionService(db, new SessionRevocationService(db), TimeProvider.System), TimeProvider.System);
        Assert.Equal(CleaningMonitorStatus.Success, (await monitor.OptionsAsync(s.Ticket, s.Sid, branch, default)).Status);
        var other = await db.Branches.Where(b => b.IsActive && b.BranchId != branch).Select(b => b.BranchId).FirstAsync();
        Assert.Equal(RoomTypeBatchStatus.Forbidden, (await s.Service.ReadInitialInspectionQueueAsync(s.Ticket, s.Sid, other, null, 1, 25, default)).Status);
        Assert.Equal(CleaningMonitorStatus.Forbidden, (await monitor.OptionsAsync(s.Ticket, s.Sid, other, default)).Status);
        var inspection = await db.RoomInspections.AsNoTracking().Where(i => i.Room.BranchId == branch && i.Room.IsActive
            && i.CleaningTaskId != null && i.InspectorUserId != s.User.UserId).FirstAsync();
        Assert.Equal(RoomTypeBatchStatus.Forbidden, (await s.Service.ExecuteInspectionAsync(s.Ticket, s.Sid, branch, inspection.InspectionId, null, "read", null, null, null, Path.GetTempPath(), default)).Status);
    }

    [HousekeepingWriterSqlFact]
    public async Task TaskExecutionCapabilityRequiresExactCurrentAssignmentAndRevokesWithPermission()
    {
        await using var db = Context(); var s = await Session(db); var branch = s.User.PrimaryBranchId!.Value;
        var task = await db.OperationalTasks.AsNoTracking().Where(t => t.BranchId == branch && t.Department.BranchId == branch
            && t.OperationalTaskAssignments.Count(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null) == 1
            && t.OperationalTaskAssignments.Any(a => a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssigneeId != s.User.UserId)).FirstAsync();
        async Task<OperationalTaskDetail> Read()
        {
            var r = await s.Service.ReadOperationalTasksAsync(s.Ticket, s.Sid, branch, new(), task.TaskId, default);
            Assert.Equal(RoomTypeBatchStatus.Success, r.Status); return Assert.IsType<OperationalTaskDetail>(r.Value);
        }
        Assert.False((await Read()).CanExecute);
        Assert.Equal(RoomTypeBatchStatus.Forbidden, (await s.Service.WorkProgressAsync(s.Ticket, s.Sid, branch, task.TaskId, false, null, null, Path.GetTempPath(), default)).Status);
        // Disposable clone only: temporarily assign the same work, then revoke the execute grant.
        await db.OperationalTaskAssignments.Where(a => a.TaskId == task.TaskId && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null)
            .ExecuteUpdateAsync(p => p.SetProperty(a => a.AssigneeId, s.User.UserId));
        Assert.True((await Read()).CanExecute);
        var grants = await db.RolePermissions.Where(p => p.Role.RoleCode == "HK_SUPERVISOR" && p.Permission.PermissionCode == "TASK_EXECUTE").ToArrayAsync();
        db.RolePermissions.RemoveRange(grants); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.False((await Read()).CanExecute);
    }
}
