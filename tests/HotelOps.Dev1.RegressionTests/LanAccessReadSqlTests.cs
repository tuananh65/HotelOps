using System.Reflection;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Assets;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

[Collection("HotelOps isolated SQL")]
public sealed class LanAccessReadSqlTests
{
    [WorkspaceReadSql]
    public async Task ExistingLanPermissionsMenuAndOwnResourcesStayScopedWithoutWrites()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = budget.Token;
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("HOTELOPS_WORKSPACE_READ_SQL")).Options);
        var lan = await db.Users.AsNoTracking().SingleAsync(u=>u.Username=="lan.dang",ct);
        var branch=lan.PrimaryBranchId!.Value;
        var actor=new Actor(lan.UserId);
        var permissions=new PermissionService(actor,new EfPermissionDataSource(db),new HotelOps.Api.Services.WorkTime.WorkdayPermissionResourceResolver(db,null!),TimeProvider.System);
        var sessions=new Sessions(lan.UserId);
        var ticket=CleaningClosureWorkflowTests.Ticket(lan.UserId);
        var auditBefore=await db.AuditLogs.AsNoTracking().CountAsync(ct);
        var intake = await new HotelOps.Api.Services.InternalRequests.InternalRequestIntakeService(db, sessions, actor)
            .OptionsAsync(ticket, 1, branch, ct);
        Assert.NotEmpty(intake.Types);
        Assert.All(intake.Types, type => Assert.False(type.MaterialNeed));
        var sourceTypes = await db.InternalRequestTypes.AsNoTracking()
            .Where(type => type.IsActive).ToArrayAsync(ct);
        foreach (var type in intake.Types)
        {
            var source = Assert.Single(sourceTypes, row => row.RequestTypeId == type.Id);
            Assert.Equal(source.TypeCode == "OTHER", type.AllowAnyDepartment);
            if (type.DepartmentId is { } department)
            {
                Assert.Equal(source.DefaultDepartmentId, department);
                Assert.Contains(intake.Departments, row => row.Id == department);
            }
        }
        Assert.Contains(intake.Types, type => type.AllowAnyDepartment);
        var workspace=await new RoomDirectoryService(db,actor,sessions,permissions).WorkspaceAsync(ticket,ct);
        var allowed=Assert.Single(workspace.Items!);
        Assert.Equal(branch,allowed.Id);Assert.True(allowed.CanReadOwn);Assert.True(allowed.CanViewRooms);
        Assert.False(allowed.CanCreate);Assert.False(allowed.CanMonitor);
        var access=new InsightAccess(db,permissions,actor);
        var navigation=new HomeNavigationService(db,actor,permissions,TimeProvider.System,
            new AuditAccess(db,access,TimeProvider.System),new AssetMaintenanceHistoryReader(db,access,null!),
            new AssetLocationReview(db,access,null!),new AssetProfileAccess(db,access,null!));
        var links=await (Task<string[]>)typeof(HomeNavigationService).GetMethod("LinksAsync",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(navigation,[ct])!;
        foreach(var path in new[]{"/cleaning/tasks","/rework","/internal-requests","/internal-requests/new","/operations/material-requests","/material-requests/new","/incidents/new","/operational-tasks","/workday","/my-time-log","/time-corrections"}) Assert.Contains(path,links);
        foreach(var path in new[]{"/dashboard","/users","/roles","/inspection-history","/inspections/assignment","/cleaning/assignment","/rooms/turnover","/operations/incidents","/operations/maintenance","/inventory/stock","/assets","/inventory/transfer-requests","/workday/shift-plans"}) Assert.DoesNotContain(path,links);
        var service=new RoomTypeCreationService(db,actor,permissions,sessions,TimeProvider.System);
        var assigned = await db.CleaningAssignments.AsNoTracking().Where(a => a.AssigneeUserId == lan.UserId
            && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.CleaningTask.Room.BranchId == branch)
            .OrderBy(a => a.AssignmentId).FirstAsync(ct);
        var cleaningRecord = await service.ReadOwnCleaningRecordAsync(ticket, 1, branch, assigned.CleaningTaskId, ct);
        Assert.Equal(RoomTypeBatchStatus.Success, cleaningRecord.Status);
        var cleaningDetail = Assert.IsType<AssignedCleaningRecordDto>(cleaningRecord.Value);
        Assert.Equal(assigned.CleaningTaskId, cleaningDetail.Id);
        Assert.Equal(branch, cleaningDetail.BranchId);
        if (cleaningDetail.DueAt is { } deadline) Assert.Equal(DateTimeKind.Utc, deadline.Kind);
        Assert.Equal(RoomTypeBatchStatus.Forbidden,
            (await service.ReadOwnCleaningRecordAsync(ticket, 1, long.MaxValue, assigned.CleaningTaskId, ct)).Status);
        var otherTask = await db.CleaningTasks.AsNoTracking().Where(t => t.Room.BranchId == branch
            && !db.CleaningAssignments.Any(a => a.CleaningTaskId == t.CleaningTaskId && a.AssigneeUserId == lan.UserId
                && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null)).Select(t => t.CleaningTaskId).FirstAsync(ct);
        Assert.Equal(RoomTypeBatchStatus.Forbidden,
            (await service.ReadOwnCleaningRecordAsync(ticket, 1, branch, otherTask, ct)).Status);
        Assert.Equal(RoomTypeBatchStatus.Success,(await service.ListOwnCleaningAsync(ticket,1,branch,null,ct)).Status);
        Assert.Equal(RoomTypeBatchStatus.Success,(await service.ListOwnReworksAsync(ticket,1,branch,null,ct)).Status);
        var legacy=await db.CleaningReworks.AsNoTracking().Where(r=>r.AssigneeUserId==lan.UserId&&r.CleaningTask.Room.BranchId==branch).OrderBy(r=>r.ReworkId).FirstAsync(ct);
        var record=await service.ReadAssignedReworkRecordAsync(ticket,1,branch,legacy.ReworkId,ct);
        Assert.Equal(RoomTypeBatchStatus.Success,record.Status);
        var detail=Assert.IsType<AssignedReworkRecordDto>(record.Value);
        Assert.Equal(legacy.Reason,detail.Reason);Assert.Equal(legacy.Status,detail.RecordedStatus);
        Assert.Equal(DateTimeKind.Utc,detail.RequestedAt.Kind);
        var otherRework=await db.CleaningReworks.AsNoTracking().FirstAsync(r=>r.AssigneeUserId!=lan.UserId,ct);
        Assert.NotEqual(RoomTypeBatchStatus.Success,(await service.ReadAssignedReworkRecordAsync(ticket,1,branch,otherRework.ReworkId,ct)).Status);
        Assert.Equal(RoomTypeBatchStatus.Success,(await service.ReadOperationalTasksAsync(ticket,1,branch,new(),null,ct)).Status);
        var other=await db.CleaningAssignments.AsNoTracking().FirstAsync(a=>a.AssigneeUserId!=lan.UserId&&a.AssignmentStatus=="ACTIVE"&&a.CleaningTask.Room.BranchId==branch,ct);
        Assert.False(await service.CanReadAssignedCleaningAsync(lan.UserId,branch,other.CleaningTaskId,other.AssignmentId,ct));
        var foreign=await db.Branches.AsNoTracking().Where(b=>b.BranchId!=branch&&b.IsActive).Select(b=>b.BranchId).FirstAsync(ct);
        Assert.NotEqual(RoomTypeBatchStatus.Success,(await service.ReadAssignedReworkRecordAsync(ticket,1,foreign,legacy.ReworkId,ct)).Status);
        Assert.Equal(RoomTypeBatchStatus.Forbidden,(await service.ListOwnCleaningAsync(ticket,1,foreign,null,ct)).Status);
        Assert.Equal(RoomTypeBatchStatus.Forbidden,(await service.ReadOperationalTasksAsync(ticket,1,foreign,new(),null,ct)).Status);
        Assert.Equal(auditBefore,await db.AuditLogs.AsNoTracking().CountAsync(ct));
        Assert.False(db.ChangeTracker.HasChanges());
        var materials = await new HotelOps.Api.Services.MaterialRequests.MaterialRequestService(db, null!, null!).List(
            new HotelOps.Api.DTOs.MaterialRequests.MaterialPage { BranchId = branch, RequesterId = lan.UserId }, ct);
        Assert.Contains(materials.Items, r => !r.CanOpenDetail);
        Assert.Contains(materials.Items, r => r.CanOpenDetail);
        foreach (var item in materials.Items) Assert.Equal(lan.UserId, item.RequesterId);
    }
    private sealed class Actor(long user):ICurrentUserAccessor
    {public bool TryGetCurrentUser(out CurrentUserInfo value){value=new(user,1,false);return true;}}
    private sealed class Sessions(long user):ISqlSessionService
    {
        public Task<SessionValidationResult> ValidateSessionAsync(SessionReference t,CancellationToken cancellationToken=default)
            =>Task.FromResult(new SessionValidationResult(true,new(1,user,t.CreatedAtUtc,DateTime.UtcNow.AddHours(1),DateTime.UtcNow.AddHours(2),false)));
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult c,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference t,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference t,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long u,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
}
