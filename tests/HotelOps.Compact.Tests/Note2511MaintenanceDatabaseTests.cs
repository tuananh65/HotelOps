using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Compact.Tests;

// Real SQL fixture restricted to the verified disposable clone; every scenario rolls back.
[Collection("Note2511 SQL")]
public sealed class Note2511MaintenanceDatabaseTests
{
    private sealed class FixtureContext: IHttpContextAccessor { public HttpContext? HttpContext {get;set;} }
    private sealed class Clock(DateTime now):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(now,TimeSpan.Zero); }
    private static HotelOpsCompactDbContext Open()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_NOTE2511_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNote2511260145",new SqlConnectionStringBuilder(connection).InitialCatalog);
        return new(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
    }
    private static async Task<(long User,long Branch,long Department,DateTime Now)> Attendance(HotelOpsCompactDbContext db,string role,long? branch,CancellationToken ct)
    {
        var candidate=await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
            join ur in db.UserRoles on r.RoleId equals ur.RoleId join u in db.Users on ur.UserId equals u.UserId
            where r.Code==role&&r.IsActive&&s.IsActive&&s.StartMinute<=550&&s.EndMinute>550&&(branch==null||s.BranchId==branch)
                &&ur.BranchId==s.BranchId&&ur.RevokedAt==null&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
                &&db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==s.BranchId&&m.DepartmentId==s.DepartmentId&&m.Kind=="EMPLOYMENT"&&m.ValidTo==null)
            orderby s.BranchId,u.UserId select new{s.ShiftId,s.BranchId,s.DepartmentId,u.UserId}).FirstAsync(ct);
        var date=new DateOnly(2026,12,2);
        var zone=await db.Branches.Where(b=>b.BranchId==candidate.BranchId).Select(b=>b.Timezone).SingleAsync(ct);
        var now=TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(9,10)),TimeZoneInfo.FindSystemTimeZoneById(zone));
        db.WorkdayAssignments.Add(new WorkdayAssignment{ShiftId=candidate.ShiftId,UserId=candidate.UserId,WorkDate=date,CreatedBy=candidate.UserId,CreatedAt=now,Status="WORKING",StartedAt=now});
        await db.SaveChangesAsync(ct);return(candidate.UserId,candidate.BranchId,candidate.DepartmentId,now);
    }
    private static async Task<WorkService> Service(HotelOpsCompactDbContext db,long userId,DateTime now,CancellationToken ct)
    {
        var user=await db.Users.SingleAsync(u=>u.UserId==userId,ct);
        var session=new UserSession{UserId=userId,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=user.SecurityStamp,CreatedAt=now,ExpiresAt=now.AddHours(2)};
        db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,userId.ToString()),new Claim("session_id",session.SessionId.ToString())],"fixture"))};
        context.Request.Method="POST";context.Request.Path="/api/v2/work";
        var actor=new Actor(new FixtureContext{HttpContext=context});var clock=new Clock(now);var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
        var notify=new NotificationService(db,actor,access,atomic,clock);var documents=new DocumentService(db,access,actor,atomic,audit,clock,notify);
        var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var requests=new RequestWorkflowService(db,access,actor,atomic,audit,clock,supplies,documents,notify);
        return new(db,access,actor,atomic,audit,clock,notify,documents,new RoomReadinessService(db,actor,clock),supplies,requests);
    }
    private static async Task<(WorkItem Work,WorkItem Incident,Asset Asset)> Work(HotelOpsCompactDbContext db,long user,long branch,long department,DateTime now,CancellationToken ct)
    {
        var asset=await db.Assets.FirstAsync(a=>a.BranchId==branch&&a.IsActive&&a.RoomId!=null,ct);
        var incident=new WorkItem{BranchId=branch,Kind="INCIDENT",Status="IN_PROGRESS",WorkNo=Guid.NewGuid().ToString("N"),Title="Isolated maintenance incident",AssetId=asset.AssetId,RoomId=asset.RoomId,DepartmentId=department,CreatedBy=user,CreatedAt=now};
        db.WorkItems.Add(incident);await db.SaveChangesAsync(ct);
        var work=new WorkItem{BranchId=branch,Kind="MAINTENANCE",Status="IN_PROGRESS",WorkNo=Guid.NewGuid().ToString("N"),Title="Isolated maintenance attempt",AssetId=asset.AssetId,RoomId=asset.RoomId,ParentWorkId=incident.WorkId,DepartmentId=department,CreatedBy=user,CreatedAt=now,ActualStartAt=now};
        db.WorkItems.Add(work);await db.SaveChangesAsync(ct);
        db.WorkAssignments.Add(new WorkAssignment{BranchId=branch,WorkId=work.WorkId,AssigneeUserId=user,AssignedBy=user,AssignmentStatus="ACTIVE",AssignedAt=now,AcceptedAt=now});
        await db.SaveChangesAsync(ct);return(work,incident,asset);
    }
    [Note2511DatabaseFact]
    public async Task SuccessfulRepairQueuesHousekeepingInspectionWithoutReleasingRoom()
    {
        await using var db=Open();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var staff=await Attendance(db,"MAINTENANCE_STAFF",null,ct);
        var (work,incident,asset)=await Work(db,staff.User,staff.Branch,staff.Department,staff.Now,ct);
        db.WorkEvents.Add(new WorkEvent{BranchId=staff.Branch,WorkId=incident.WorkId,ActorUserId=staff.User,OperationKey=Guid.NewGuid(),OccurredAt=staff.Now,
            EventType="ROOM_ISSUE_REPORTED",SnapshotJson="{\"Kind\":\"BROKEN\",\"ObjectName\":\"Fixture asset\"}"});
        await db.SaveChangesAsync(ct);
        db.WorkEvents.Add(new WorkEvent{BranchId=staff.Branch,WorkId=incident.WorkId,ActorUserId=staff.User,OperationKey=Guid.NewGuid(),OccurredAt=staff.Now,EventType="ROOM_ISSUE_REPAIR_CREATED"});
        await db.SaveChangesAsync(ct);
        var before=await db.Rooms.Where(r=>r.RoomId==work.RoomId).Select(r=>r.OperationalStatus).SingleAsync(ct);
        var service=await Service(db,staff.User,staff.Now,ct);
        await CompleteChecklist(db,service,work,ct);
        await service.Act(work.WorkId,"complete",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Đã sửa chữa và thử hoạt động"),ct);
        Assert.Equal("COMPLETED",work.Status);Assert.Equal("PASS",work.Result);Assert.Equal("RESOLVED",incident.Status);
        var flow=await db.RequestWorkflows.SingleAsync(r=>r.WorkId==incident.WorkId,ct);
        Assert.Equal("PENDING_REINSPECTION",flow.Status);
        Assert.Equal("HOUSEKEEPING_SUPERVISOR",await db.Roles.Where(r=>r.RoleId==flow.QueueRoleId).Select(r=>r.Code).SingleAsync(ct));
        Assert.Equal(before,await db.Rooms.Where(r=>r.RoomId==work.RoomId).Select(r=>r.OperationalStatus).SingleAsync(ct));
        Assert.True(await db.Notifications.AnyAsync(n=>n.WorkId==incident.WorkId&&n.BranchId==staff.Branch
            &&db.UserRoles.Any(ur=>ur.UserId==n.RecipientUserId&&ur.BranchId==staff.Branch&&db.Roles.Any(r=>r.RoleId==ur.RoleId&&r.Code=="HOUSEKEEPING_SUPERVISOR")),ct));
        Assert.False(await db.WorkAssignments.AnyAsync(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE",ct));
        await service.Detail(work.WorkId,ct);
        await tx.RollbackAsync(ct);
    }
    private static async Task CompleteChecklist(HotelOpsCompactDbContext db,WorkService service,WorkItem work,CancellationToken ct)
    {
        if(work.ChecklistTemplateId==null){
            var template=new ChecklistTemplate{BranchId=work.BranchId,Code="FIXTURE-"+Guid.NewGuid().ToString("N"),VersionNo=1,Name="Kiểm thử bảo trì",Kind="MAINTENANCE",IsActive=true,CreatedBy=work.CreatedBy,CreatedAt=work.CreatedAt};
            db.ChecklistTemplates.Add(template);await db.SaveChangesAsync(ct);
            db.ChecklistItems.Add(new ChecklistItem{ChecklistTemplateId=template.ChecklistTemplateId,LineNo=1,Description="Kiểm tra thiết bị hoạt động",IsRequired=true,Weight=1});
            await db.SaveChangesAsync(ct);template.PublishedAt=work.CreatedAt;await db.SaveChangesAsync(ct);
            await service.AttachExecutionChecklist(work.WorkId,new(Convert.ToBase64String(work.RowVersion),template.ChecklistTemplateId),ct);
        }
        foreach(var item in await db.WorkChecklistItems.Where(i=>i.WorkId==work.WorkId).ToArrayAsync(ct))
            await service.Checklist(work.WorkId,item.WorkChecklistItemId,new(Convert.ToBase64String(item.RowVersion),"PASS",null),ct);
    }
    [Note2511DatabaseFact]
    public async Task UnrepairableAttemptRemainsFailedWhileApprovalCreatesAssignableFollowup()
    {
        await using var db=Open();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_getapplock @Resource=N'Note2511MaintenanceFixtures', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=60000",ct);
        var staff=await Attendance(db,"MAINTENANCE_STAFF",null,ct);var lead=await Attendance(db,"MAINTENANCE_SUPERVISOR",staff.Branch,ct);
        var (work,incident,asset)=await Work(db,staff.User,staff.Branch,staff.Department,staff.Now,ct);
        var roomStatus=await db.Rooms.Where(r=>r.RoomId==work.RoomId).Select(r=>r.OperationalStatus).SingleAsync(ct);
        var worker=await Service(db,staff.User,staff.Now,ct);
        var outcome=new ActionRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Không thể sửa chữa trong kiểm thử SQL");
        await worker.Act(work.WorkId,"unrepairable",outcome,ct);
        Assert.Equal("COMPLETED",work.Status);Assert.Equal("FAIL",work.Result);Assert.Equal("FAULTY",asset.ConditionCode);Assert.Equal("IN_PROGRESS",incident.Status);
        await worker.Act(work.WorkId,"unrepairable",outcome,ct);
        Assert.Equal(1,await db.WorkEvents.CountAsync(e=>e.WorkId==work.WorkId&&e.EventType=="MAINTENANCE_UNREPAIRABLE",ct));
        var supervisor=await Service(db,lead.User,staff.Now,ct);
        await supervisor.ReplaceAsset(work.WorkId,"propose",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Hỏng không thể khắc phục","Thay thiết bị cùng loại"),ct);
        var managerId=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId join u in db.Users on ur.UserId equals u.UserId
            where r.Code=="OPERATION_MANAGER"&&ur.BranchId==staff.Branch&&ur.RevokedAt==null&&u.AccountStatus=="ACTIVE" select u.UserId).FirstAsync(ct);
        var manager=await Service(db,managerId,staff.Now,ct);
        await manager.ReplaceAsset(work.WorkId,"approve",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Duyệt thay thế trong kiểm thử SQL"),ct);
        var link=await db.WorkLinks.SingleAsync(l=>l.TargetWorkId==work.WorkId&&l.Relation=="SOURCE",ct);
        var followup=await db.WorkItems.SingleAsync(w=>w.WorkId==link.SourceWorkId,ct);
        Assert.Equal("PENDING",followup.Status);Assert.Equal(incident.WorkId,followup.ParentWorkId);Assert.Equal(asset.AssetId,followup.AssetId);
        Assert.Equal("COMPLETED",work.Status);Assert.Equal("FAIL",work.Result);
        Assert.True(await db.WorkEvents.AnyAsync(e=>e.WorkId==followup.WorkId&&e.EventType=="REPLACEMENT_APPROVED",ct));
        await supervisor.Act(followup.WorkId,"assign",new(Convert.ToBase64String(followup.RowVersion),Guid.NewGuid(),UserId:staff.User),ct);
        await worker.Act(followup.WorkId,"accept",new(Convert.ToBase64String(followup.RowVersion),Guid.NewGuid()),ct);
        await worker.Act(followup.WorkId,"start",new(Convert.ToBase64String(followup.RowVersion),Guid.NewGuid()),ct);
        var replacement=new Asset{BranchId=staff.Branch,AssetCode="FIXTURE-"+Guid.NewGuid().ToString("N"),Name="Thiết bị thay thế kiểm thử",RoomId=asset.RoomId,
            AssetTypeId=asset.AssetTypeId,ConditionCode="GOOD",IsActive=true,CreatedAt=staff.Now};
        asset.RoomId=null;db.Assets.Add(replacement);await db.SaveChangesAsync(ct);
        await worker.ReplaceAsset(followup.WorkId,"install",new(Convert.ToBase64String(followup.RowVersion),Guid.NewGuid(),"Đã lắp thiết bị thay thế",ReplacementAssetId:replacement.AssetId),ct);
        await CompleteChecklist(db,worker,followup,ct);
        await worker.Act(followup.WorkId,"complete",new(Convert.ToBase64String(followup.RowVersion),Guid.NewGuid(),"Thiết bị mới hoạt động tốt"),ct);
        Assert.Equal("COMPLETED",followup.Status);Assert.Equal("PASS",followup.Result);Assert.Equal("FAIL",work.Result);Assert.Equal("RESOLVED",incident.Status);
        Assert.Equal(roomStatus,await db.Rooms.Where(r=>r.RoomId==work.RoomId).Select(r=>r.OperationalStatus).SingleAsync(ct));
        await tx.RollbackAsync(ct);
    }
}
