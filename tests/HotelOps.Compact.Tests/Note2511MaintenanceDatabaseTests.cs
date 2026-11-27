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
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;
using Xunit;

namespace HotelOps.Compact.Tests;

// Real SQL fixture restricted to the verified disposable clone; every scenario rolls back.
[Collection("Note2511 SQL")]
public sealed class Note2511MaintenanceDatabaseTests : IDisposable
{
    private readonly string evidenceRoot=Path.Combine(Path.GetTempPath(),"hotelops-maintenance-"+Guid.NewGuid().ToString("N"));
    private sealed class FixtureEnvironment(string root):IWebHostEnvironment
    {
        public string WebRootPath {get;set;}=root;
        public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
        public string ApplicationName {get;set;}="HotelOps.Api";
        public string EnvironmentName {get;set;}="Testing";
        public string ContentRootPath {get;set;}=root;
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
    public void Dispose(){if(Directory.Exists(evidenceRoot))Directory.Delete(evidenceRoot,true);}
    private sealed class FixtureContext: IHttpContextAccessor { public HttpContext? HttpContext {get;set;} }
    private sealed class Clock(DateTime now):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(now,TimeSpan.Zero); }
    private static HotelOpsCompactDbContext Open()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",new SqlConnectionStringBuilder(connection).InitialCatalog);
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
        // The restored clone can contain an older active shift. End it only inside
        // this rollback fixture before creating attendance at the fixture clock.
        foreach(var old in await db.WorkdayAssignments.Where(a=>a.UserId==candidate.UserId&&a.Status=="WORKING").ToArrayAsync(ct)){
            old.Status="ENDED";old.EndedAt=now;
        }
        await db.SaveChangesAsync(ct);
        db.WorkdayAssignments.Add(new WorkdayAssignment{ShiftId=candidate.ShiftId,UserId=candidate.UserId,WorkDate=date,CreatedBy=candidate.UserId,CreatedAt=now,Status="WORKING",StartedAt=now});
        await db.SaveChangesAsync(ct);return(candidate.UserId,candidate.BranchId,candidate.DepartmentId,now);
    }
    private async Task<WorkService> Service(HotelOpsCompactDbContext db,long userId,DateTime now,CancellationToken ct)
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
        return new(db,access,actor,atomic,audit,clock,notify,documents,new RoomReadinessService(db,actor,clock),supplies,requests,
            new FixtureEnvironment(evidenceRoot),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Compact:EvidenceDirectory",evidenceRoot}}).Build());
    }
    private async Task<long> Photo(HotelOpsCompactDbContext db,WorkItem work,long user,DateTime now,CancellationToken ct)
    {
        var assignment=await db.WorkAssignments.SingleAsync(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE",ct);
        var start=await db.WorkEvents.Where(e=>e.WorkId==work.WorkId&&e.EventType=="START").OrderByDescending(e=>e.WorkEventId).FirstOrDefaultAsync(ct);
        if(start==null){
            start=new WorkEvent{WorkId=work.WorkId,BranchId=work.BranchId,ActorUserId=user,EventType="START",OperationKey=Guid.NewGuid(),OccurredAt=now};
            db.WorkEvents.Add(start);await db.SaveChangesAsync(ct);
        }
        var data=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNomLAAAANEAbF61zYUAAAAAElFTkSuQmCC");
        var key=Guid.NewGuid().ToString("N");Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllBytesAsync(Path.Combine(evidenceRoot,key),data,ct);
        var attachment=new Attachment{BranchId=work.BranchId,WorkId=work.WorkId,StorageKey=key,OriginalName="repair.png",MimeType="image/png",
            ByteLength=data.Length,Sha256=SHA256.HashData(data),UploadedBy=user,CreatedAt=now};
        db.Attachments.Add(attachment);await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog{ActorUserId=user,BranchId=work.BranchId,EntityType="Attachment",EntityKey=attachment.AttachmentId.ToString(),Action="UPLOAD",
            OperationKey=Guid.NewGuid(),OccurredAt=now,AfterJson=JsonSerializer.Serialize(new{WorkId=work.WorkId,RoomId=work.RoomId,AssignmentId=assignment.AssignmentId,UserId=user,
                ExecutionEventId=start.WorkEventId,ExecutionStartedAt=start.OccurredAt})});
        await db.SaveChangesAsync(ct);return attachment.AttachmentId;
    }
    private static async Task<(WorkItem Work,WorkItem Incident,Asset Asset)> Work(HotelOpsCompactDbContext db,long user,long branch,long department,DateTime now,CancellationToken ct,bool started=true)
    {
        var asset=await db.Assets.FirstAsync(a=>a.BranchId==branch&&a.IsActive&&a.RoomId!=null,ct);
        var incident=new WorkItem{BranchId=branch,Kind="INCIDENT",Status="IN_PROGRESS",WorkNo=Guid.NewGuid().ToString("N"),Title="Isolated maintenance incident",AssetId=asset.AssetId,RoomId=asset.RoomId,DepartmentId=department,CreatedBy=user,CreatedAt=now};
        db.WorkItems.Add(incident);await db.SaveChangesAsync(ct);
        var work=new WorkItem{BranchId=branch,Kind="MAINTENANCE",Status=started?"IN_PROGRESS":"PENDING",WorkNo=Guid.NewGuid().ToString("N"),Title="Isolated maintenance attempt",AssetId=asset.AssetId,RoomId=asset.RoomId,ParentWorkId=incident.WorkId,DepartmentId=department,CreatedBy=user,CreatedAt=now,ActualStartAt=started?now:null};
        db.WorkItems.Add(work);await db.SaveChangesAsync(ct);
        db.WorkAssignments.Add(new WorkAssignment{BranchId=branch,WorkId=work.WorkId,AssigneeUserId=user,AssignedBy=user,AssignmentStatus="ACTIVE",AssignedAt=now,AcceptedAt=started?now:null});
        await db.SaveChangesAsync(ct);return(work,incident,asset);
    }
    [TaskSupplyDatabaseFact]
    public async Task MaintenanceHandoverBlocksAcceptanceAndAssignmentRechecksCurrentShift()
    {
        await using var db=Open();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var staff=await Attendance(db,"MAINTENANCE_STAFF",null,ct);var lead=await Attendance(db,"MAINTENANCE_SUPERVISOR",staff.Branch,ct);
        var (work,_,_)=await Work(db,staff.User,staff.Branch,staff.Department,staff.Now,ct,started:false);
        var worker=await Service(db,staff.User,staff.Now,ct);var supervisor=await Service(db,lead.User,staff.Now,ct);
        var candidates=JsonSerializer.SerializeToElement(await supervisor.Candidates(staff.Branch,"MAINTENANCE",ct));
        Assert.Contains(candidates.EnumerateArray(),c=>c.GetProperty("UserId").GetInt64()==staff.User);
        var request=new ActionRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Đề nghị giám sát nhận bàn giao");
        await worker.Act(work.WorkId,"request-handover",request,ct);
        await worker.Act(work.WorkId,"request-handover",request,ct);
        foreach(var action in new[]{"accept","start"}){
            var error=await Assert.ThrowsAsync<ApiException>(()=>worker.Act(work.WorkId,action,new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid()),ct));
            Assert.Equal("HANDOVER_REQUEST_PENDING",error.Code);
        }
        var handover=new ActionRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Giám sát nhận bàn giao công việc");
        await supervisor.Act(work.WorkId,"accept-handover-request",handover,ct);
        await supervisor.Act(work.WorkId,"accept-handover-request",handover,ct);
        Assert.Equal("PENDING",work.Status);
        Assert.False(await db.WorkAssignments.AnyAsync(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE",ct));
        Assert.False(await db.WorkIntervals.AnyAsync(t=>t.WorkId==work.WorkId,ct));
        await supervisor.Act(work.WorkId,"assign",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Phân công lại sau bàn giao",UserId:staff.User),ct);
        await worker.Act(work.WorkId,"accept",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid()),ct);
        var acceptedError=await Assert.ThrowsAsync<ApiException>(()=>worker.Act(work.WorkId,"request-handover",
            new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Không được bàn giao sau khi nhận"),ct));
        Assert.Equal("MAINTENANCE_HANDOVER_BEFORE_ACCEPT_ONLY",acceptedError.Code);
        var day=await db.WorkdayAssignments.SingleAsync(a=>a.UserId==staff.User&&a.WorkDate==new DateOnly(2026,12,2)&&a.Status=="WORKING",ct);
        day.Status="ENDED";day.EndedAt=staff.Now;await db.SaveChangesAsync(ct);
        candidates=JsonSerializer.SerializeToElement(await supervisor.Candidates(staff.Branch,"MAINTENANCE",ct));
        Assert.DoesNotContain(candidates.EnumerateArray(),c=>c.GetProperty("UserId").GetInt64()==staff.User);
        var offShift=await Assert.ThrowsAsync<ApiException>(()=>supervisor.Act(work.WorkId,"assign",
            new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Từ chối phân người đã hết ca",UserId:staff.User),ct));
        Assert.Equal("MAINTENANCE_ASSIGNEE_NOT_ON_SHIFT",offShift.Code);
        await tx.RollbackAsync(ct);
    }
    [TaskSupplyDatabaseFact]
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
        await AttachIncompleteChecklist(db,service,work,ct);
        var missingPhoto=await Assert.ThrowsAsync<ApiException>(()=>service.Act(work.WorkId,"complete",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Đã sửa chữa và thử hoạt động"),ct));
        Assert.Equal("MAINTENANCE_PHOTO_REQUIRED",missingPhoto.Code);
        var photo=await Photo(db,work,staff.User,staff.Now,ct);
        var completion=new ActionRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Đã sửa chữa và thử hoạt động",AttachmentIds:[photo]);
        await service.Act(work.WorkId,"complete",completion,ct);
        await service.Act(work.WorkId,"complete",completion,ct);
        Assert.Equal(1,await db.WorkEvents.CountAsync(e=>e.WorkId==work.WorkId&&e.EventType=="COMPLETE",ct));
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
    private static async Task AttachIncompleteChecklist(HotelOpsCompactDbContext db,WorkService service,WorkItem work,CancellationToken ct)
    {
        if(work.ChecklistTemplateId==null){
            var template=new ChecklistTemplate{BranchId=work.BranchId,Code="FIXTURE-"+Guid.NewGuid().ToString("N"),VersionNo=1,Name="Kiểm thử bảo trì",Kind="MAINTENANCE",IsActive=true,CreatedBy=work.CreatedBy,CreatedAt=work.CreatedAt};
            db.ChecklistTemplates.Add(template);await db.SaveChangesAsync(ct);
            db.ChecklistItems.Add(new ChecklistItem{ChecklistTemplateId=template.ChecklistTemplateId,LineNo=1,Description="Kiểm tra thiết bị hoạt động",IsRequired=true,Weight=1});
            await db.SaveChangesAsync(ct);template.PublishedAt=work.CreatedAt;await db.SaveChangesAsync(ct);
            await service.AttachExecutionChecklist(work.WorkId,new(Convert.ToBase64String(work.RowVersion),template.ChecklistTemplateId),ct);
        }
        Assert.True(await db.WorkChecklistItems.AnyAsync(i=>i.WorkId==work.WorkId&&i.RequiredSnapshot&&i.Outcome==null,ct));
    }
    [TaskSupplyDatabaseFact]
    public async Task FailedRepairStaysReadableAndManagerReplacementRequestsRoomInspection()
    {
        await using var db=Open();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_getapplock @Resource=N'Note2511MaintenanceFixtures', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=60000",ct);
        var staff=await Attendance(db,"MAINTENANCE_STAFF",null,ct);var lead=await Attendance(db,"MAINTENANCE_SUPERVISOR",staff.Branch,ct);
        var (work,incident,asset)=await Work(db,staff.User,staff.Branch,staff.Department,staff.Now,ct);
        work.CreatedBy=lead.User;
        var correlation=Guid.NewGuid();
        var leadRole=await db.Roles.Where(r=>r.Code=="MAINTENANCE_SUPERVISOR").Select(r=>r.RoleId).SingleAsync(ct);
        db.RequestWorkflows.AddRange(
            new RequestWorkflow{WorkId=incident.WorkId,BranchId=staff.Branch,RequestType="ROOM_EQUIPMENT_DAMAGE",Status="IN_PROGRESS",QueueRoleId=leadRole,CorrelationId=correlation,UpdatedAt=staff.Now},
            new RequestWorkflow{WorkId=work.WorkId,BranchId=staff.Branch,RequestType="MAINTENANCE_REMEDIATION",Status="IN_PROGRESS",QueueRoleId=leadRole,ParentRequestWorkId=incident.WorkId,CorrelationId=correlation,UpdatedAt=staff.Now});
        db.WorkEvents.Add(new WorkEvent{WorkId=incident.WorkId,BranchId=staff.Branch,EventType="ROOM_ISSUE_REPORTED",ActorUserId=lead.User,OperationKey=Guid.NewGuid(),OccurredAt=staff.Now,
            SnapshotJson=JsonSerializer.Serialize(new{Kind="BROKEN",ItemId=(long?)null,ObjectName=asset.Name,Quantity=1,Impact="READINESS"})});
        await db.SaveChangesAsync(ct);
        db.WorkEvents.Add(new WorkEvent{WorkId=incident.WorkId,BranchId=staff.Branch,EventType="ROOM_ISSUE_REPAIR_CREATED",ActorUserId=lead.User,OperationKey=Guid.NewGuid(),OccurredAt=staff.Now});
        await db.SaveChangesAsync(ct);
        var roomStatus=await db.Rooms.Where(r=>r.RoomId==work.RoomId).Select(r=>r.OperationalStatus).SingleAsync(ct);
        var worker=await Service(db,staff.User,staff.Now,ct);
        var photo=await Photo(db,work,staff.User,staff.Now,ct);
        var outcome=new ActionRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Không thể sửa chữa trong kiểm thử SQL",AttachmentIds:[photo]);
        await worker.Act(work.WorkId,"unrepairable",outcome,ct);
        Assert.Equal("COMPLETED",work.Status);Assert.Equal("FAIL",work.Result);Assert.Equal("FAULTY",asset.ConditionCode);Assert.Equal("IN_PROGRESS",incident.Status);
        await worker.Detail(work.WorkId,ct);
        await worker.ReplacementDetail(work.WorkId,ct);
        await worker.Act(work.WorkId,"unrepairable",outcome,ct);
        Assert.Equal(1,await db.WorkEvents.CountAsync(e=>e.WorkId==work.WorkId&&e.EventType=="MAINTENANCE_UNREPAIRABLE",ct));
        var supervisor=await Service(db,lead.User,staff.Now,ct);
        await supervisor.ReplaceAsset(work.WorkId,"propose",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Hỏng không thể khắc phục","Thay thiết bị cùng loại"),ct);
        var managerId=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId join u in db.Users on ur.UserId equals u.UserId
            where r.Code=="OPERATION_MANAGER"&&ur.BranchId==staff.Branch&&ur.RevokedAt==null&&u.AccountStatus=="ACTIVE" select u.UserId).FirstAsync(ct);
        var manager=await Service(db,managerId,staff.Now,ct);
        await manager.ReplaceAsset(work.WorkId,"approve",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid()),ct);
        Assert.False(await db.WorkLinks.AnyAsync(l=>l.TargetWorkId==work.WorkId&&l.Relation=="SOURCE",ct));
        Assert.Equal("COMPLETED",work.Status);Assert.Equal("FAIL",work.Result);
        var replacement=new Asset{BranchId=staff.Branch,AssetCode="FIXTURE-"+Guid.NewGuid().ToString("N"),Name="Thiết bị thay thế kiểm thử",
            AssetTypeId=asset.AssetTypeId,ConditionCode="GOOD",IsActive=true,CreatedAt=staff.Now};
        db.Assets.Add(replacement);await db.SaveChangesAsync(ct);
        var install=new ReplacementRequest(Convert.ToBase64String(work.RowVersion),Guid.NewGuid());
        await manager.ReplaceAsset(work.WorkId,"install",install,ct);
        await manager.ReplaceAsset(work.WorkId,"install",install,ct);
        var link=await db.WorkLinks.SingleAsync(l=>l.TargetWorkId==work.WorkId&&l.Relation=="SOURCE",ct);
        var followup=await db.WorkItems.SingleAsync(w=>w.WorkId==link.SourceWorkId,ct);
        Assert.Equal(work.RoomId,asset.RoomId);Assert.Equal("GOOD",asset.ConditionCode);Assert.Null(replacement.RoomId);
        Assert.Equal(1,await db.RequestWorkflows.CountAsync(f=>f.ParentRequestWorkId==incident.WorkId&&f.RequestType=="ROOM_REINSPECTION"&&f.Status=="PENDING_REINSPECTION",ct));
        Assert.True(await db.Notifications.AnyAsync(n=>n.WorkId==incident.WorkId&&n.BranchId==staff.Branch&&n.Title.Contains("cần kiểm tra lại phòng")
            &&db.UserRoles.Any(ur=>ur.UserId==n.RecipientUserId&&ur.BranchId==staff.Branch&&db.Roles.Any(r=>r.RoleId==ur.RoleId&&r.Code=="HOUSEKEEPING_SUPERVISOR")),ct));
        Assert.Equal("COMPLETED",followup.Status);Assert.Equal("PASS",followup.Result);Assert.Equal("FAIL",work.Result);Assert.Equal("RESOLVED",incident.Status);
        Assert.Equal(roomStatus,await db.Rooms.Where(r=>r.RoomId==work.RoomId).Select(r=>r.OperationalStatus).SingleAsync(ct));
        await tx.RollbackAsync(ct);
    }
}
