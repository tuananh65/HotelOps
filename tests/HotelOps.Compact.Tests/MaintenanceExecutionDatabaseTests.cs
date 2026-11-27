using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace HotelOps.Compact.Tests;

[Collection("Note2511 SQL")]
public sealed class MaintenanceExecutionDatabaseTests
{
    private sealed class Accessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,11,27,2,10,0,TimeSpan.Zero); }
    private sealed class EnvironmentStub(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; }="HotelOps.Api";
        public string EnvironmentName { get; set; }="Test";
        public string ContentRootPath { get; set; }=root;
        public string WebRootPath { get; set; }=root;
        public IFileProvider ContentRootFileProvider { get; set; }=new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; }=new NullFileProvider();
    }
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
    private static ActionRequest Action(WorkItem work,string? reason="SQL rollback verification",long? user=null,long[]? photos=null)=>new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),reason,UserId:user,AttachmentIds:photos);
    private static HotelOpsCompactDbContext Open()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        var builder=new SqlConnectionStringBuilder(connection);
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",builder.InitialCatalog);Assert.True(builder.IntegratedSecurity);
        return new(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
    }
    private static async Task<(long User,WorkdayShift Shift,WorkdayAssignment Day)> Attendance(HotelOpsCompactDbContext db,string role,long? branch,CancellationToken ct,long? except=null)
    {
        var now=new Clock().GetUtcNow().UtcDateTime;
        var candidate=await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
            join ur in db.UserRoles on r.RoleId equals ur.RoleId join u in db.Users on ur.UserId equals u.UserId
            where r.Code==role&&r.IsActive&&s.IsActive&&s.StartMinute<=550&&s.EndMinute>550&&(branch==null||s.BranchId==branch)
                &&ur.BranchId==s.BranchId&&ur.RevokedAt==null&&ur.AssignedAt<=now&&u.UserId!=except&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
                &&db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==s.BranchId&&m.DepartmentId==s.DepartmentId&&m.Kind=="EMPLOYMENT"&&m.ValidFrom<=now&&m.ValidTo==null)
            orderby s.BranchId,u.UserId select new{s,u.UserId}).FirstAsync(ct);
        var date=new DateOnly(2026,11,27);
        var day=await db.WorkdayAssignments.SingleOrDefaultAsync(a=>a.ShiftId==candidate.s.ShiftId&&a.UserId==candidate.UserId&&a.WorkDate==date,ct);
        if(day==null){day=new(){ShiftId=candidate.s.ShiftId,UserId=candidate.UserId,WorkDate=date,CreatedBy=candidate.UserId,CreatedAt=now};db.WorkdayAssignments.Add(day);}
        day.Status="WORKING";day.StartedAt=now;day.EndedAt=null;await db.SaveChangesAsync(ct);
        return(candidate.UserId,candidate.s,day);
    }
    private static async Task<(WorkService Work,DocumentService Documents,EvidenceService Evidence)> Services(HotelOpsCompactDbContext db,long id,string root,CancellationToken ct)
    {
        var clock=new Clock();var now=clock.GetUtcNow().UtcDateTime;var user=await db.Users.SingleAsync(u=>u.UserId==id,ct);
        var session=new UserSession{UserId=id,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=user.SecurityStamp,CreatedAt=now,ExpiresAt=now.AddDays(1)};
        db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
        var actor=new Actor(new Accessor{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim("session_id",session.SessionId.ToString())],"sql-fixture"))}});
        var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);var notifications=new NotificationService(db,actor,access,atomic,clock);
        var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var requests=new RequestWorkflowService(db,access,actor,atomic,audit,clock,supplies,documents,notifications);
        var environment=new EnvironmentStub(root);var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Compact:EvidenceDirectory"]=root}).Build();
        var work=new WorkService(db,access,actor,atomic,audit,clock,notifications,documents,new(db,actor,clock),supplies,requests,environment,config);
        return(work,documents,new(db,access,actor,atomic,audit,work,documents,environment,config,clock));
    }
    private static async Task<WorkItem> NewWork(HotelOpsCompactDbContext db,long user,WorkdayShift shift,CancellationToken ct)
    {
        var asset=await db.Assets.FirstAsync(a=>a.BranchId==shift.BranchId&&a.IsActive&&a.RoomId!=null,ct);
        var work=new WorkItem{BranchId=shift.BranchId,Kind="MAINTENANCE",Status="PENDING",WorkNo=Guid.NewGuid().ToString("N"),Title="Rollback maintenance execution",AssetId=asset.AssetId,RoomId=asset.RoomId,DepartmentId=shift.DepartmentId,CreatedBy=user,CreatedAt=new Clock().GetUtcNow().UtcDateTime};
        db.WorkItems.Add(work);await db.SaveChangesAsync(ct);return work;
    }
    private static async Task Lock(HotelOpsCompactDbContext db,CancellationToken ct)=>await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.StaffSupplies',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);

    [TaskSupplyDatabaseFact]
    public async Task ShiftEligibilityIdleOrderAndSupervisorHandoverAreEnforcedInSql()
    {
        await using var db=Open();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        var initial=await db.WorkItems.CountAsync(ct);await using var tx=await db.Database.BeginTransactionAsync(ct);await Lock(db,ct);
        var staff=await Attendance(db,"MAINTENANCE_STAFF",null,ct);var lead=await Attendance(db,"MAINTENANCE_SUPERVISOR",staff.Shift.BranchId,ct);
        var second=await Attendance(db,"MAINTENANCE_STAFF",staff.Shift.BranchId,ct,staff.User);
        var root=Path.Combine(Path.GetTempPath(),"HotelOps-maintenance-sql",Guid.NewGuid().ToString("N"));
        var worker=await Services(db,staff.User,root,ct);var supervisor=await Services(db,lead.User,root,ct);
        var work=await NewWork(db,lead.User,staff.Shift,ct);
        staff.Day.Status="PLANNED";staff.Day.StartedAt=null;await db.SaveChangesAsync(ct);
        Assert.DoesNotContain(Json(await supervisor.Work.Candidates(staff.Shift.BranchId,"MAINTENANCE",ct)).EnumerateArray(),x=>x.GetProperty("userId").GetInt64()==staff.User);
        Assert.Equal("MAINTENANCE_ASSIGNEE_NOT_ON_SHIFT",(await Assert.ThrowsAsync<ApiException>(()=>supervisor.Work.Act(work.WorkId,"assign",Action(work,user:staff.User),ct))).Code);
        staff.Day.Status="WORKING";staff.Day.StartedAt=new Clock().GetUtcNow().UtcDateTime;await db.SaveChangesAsync(ct);
        await supervisor.Work.Act(work.WorkId,"assign",Action(work,user:staff.User),ct);
        await worker.Work.Act(work.WorkId,"request-handover",Action(work),ct);
        foreach(var action in new[]{"accept","start"})Assert.Equal("HANDOVER_REQUEST_PENDING",(await Assert.ThrowsAsync<ApiException>(()=>worker.Work.Act(work.WorkId,action,Action(work),ct))).Code);
        await supervisor.Work.Act(work.WorkId,"accept-handover-request",Action(work),ct);
        Assert.Equal("PENDING",work.Status);Assert.False(await db.WorkAssignments.AnyAsync(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE",ct));
        await supervisor.Work.Act(work.WorkId,"assign",Action(work,user:staff.User),ct);
        var acceptance=Action(work,reason:null);
        await worker.Work.Act(work.WorkId,"accept",acceptance,ct);
        await worker.Work.Act(work.WorkId,"accept",acceptance,ct);
        Assert.Equal("IN_PROGRESS",work.Status);
        Assert.NotNull(work.ActualStartAt);
        Assert.Equal(1,await db.WorkIntervals.CountAsync(t=>t.WorkId==work.WorkId&&t.ClosedAt==null,ct));
        Assert.Equal(1,await db.WorkEvents.CountAsync(e=>e.WorkId==work.WorkId&&e.EventType=="START",ct));
        Assert.Equal(1,await db.WorkEvents.CountAsync(e=>e.WorkId==work.WorkId&&e.EventType=="ACCEPT",ct));
        Assert.Equal("MAINTENANCE_HANDOVER_BEFORE_ACCEPT_ONLY",(await Assert.ThrowsAsync<ApiException>(()=>worker.Work.Act(work.WorkId,"request-handover",Action(work),ct))).Code);
        var nextWork=await NewWork(db,lead.User,staff.Shift,ct);
        await supervisor.Work.Act(nextWork.WorkId,"assign",Action(nextWork,user:staff.User),ct);
        Assert.Equal("ACTIVE_WORK_IN_PROGRESS",(await Assert.ThrowsAsync<ApiException>(()=>worker.Work.Act(nextWork.WorkId,"accept",Action(nextWork,reason:null),ct))).Code);
        Assert.Equal("PENDING",nextWork.Status);
        Assert.Null((await db.WorkAssignments.SingleAsync(a=>a.WorkId==nextWork.WorkId&&a.AssignmentStatus=="ACTIVE",ct)).AcceptedAt);
        Assert.False(await db.WorkIntervals.AnyAsync(t=>t.WorkId==nextWork.WorkId,ct));
        Assert.False(await db.WorkEvents.AnyAsync(e=>e.WorkId==nextWork.WorkId&&(e.EventType=="START"||e.EventType=="ACCEPT"),ct));
        var candidates=Json(await supervisor.Work.Candidates(staff.Shift.BranchId,"MAINTENANCE",ct)).EnumerateArray().ToArray();
        Assert.True(candidates.Single(x=>x.GetProperty("userId").GetInt64()==staff.User).GetProperty("busy").GetBoolean());
        Assert.False(candidates.Single(x=>x.GetProperty("userId").GetInt64()==second.User).GetProperty("busy").GetBoolean());
        Assert.True(Array.FindIndex(candidates,x=>x.GetProperty("userId").GetInt64()==second.User)<Array.FindIndex(candidates,x=>x.GetProperty("userId").GetInt64()==staff.User));
        await tx.RollbackAsync(ct);db.ChangeTracker.Clear();Assert.Equal(initial,await db.WorkItems.CountAsync(ct));
    }

    [TaskSupplyDatabaseFact]
    public async Task RealPhotosGateBothOutcomesAndFailureRoutesThroughLeadToManager()
    {
        await using var db=Open();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        var initial=await db.WorkItems.CountAsync(ct);await using var tx=await db.Database.BeginTransactionAsync(ct);await Lock(db,ct);
        var staff=await Attendance(db,"MAINTENANCE_STAFF",null,ct);var lead=await Attendance(db,"MAINTENANCE_SUPERVISOR",staff.Shift.BranchId,ct);
        var root=Path.Combine(Path.GetTempPath(),"HotelOps-maintenance-sql",Guid.NewGuid().ToString("N"));
        try{
            var worker=await Services(db,staff.User,root,ct);var supervisor=await Services(db,lead.User,root,ct);
            var managerId=await(from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId join u in db.Users on ur.UserId equals u.UserId where ur.BranchId==staff.Shift.BranchId&&ur.RevokedAt==null&&r.Code=="OPERATION_MANAGER"&&u.AccountStatus=="ACTIVE" select u.UserId).FirstAsync(ct);
            var manager=await Services(db,managerId,root,ct);
            var listing=Json(await manager.Documents.StaffIssues(staff.Shift.BranchId,1,100,null,null,null,ct));
            Assert.True(listing.GetProperty("total").GetInt32()>0);Assert.NotEmpty(listing.GetProperty("items").EnumerateArray());
            var empty=Json(await manager.Documents.StaffIssues(staff.Shift.BranchId,1,25,"no-match-"+Guid.NewGuid(),null,null,ct));Assert.Equal(0,empty.GetProperty("total").GetInt32());
            foreach(var outcome in new[]{"complete","unrepairable"}){
                var work=await NewWork(db,lead.User,staff.Shift,ct);
                await supervisor.Work.Act(work.WorkId,"assign",Action(work,user:staff.User),ct);
                await worker.Work.Act(work.WorkId,"accept",Action(work,reason:null),ct);
                Assert.Equal("MAINTENANCE_PHOTO_REQUIRED",(await Assert.ThrowsAsync<ApiException>(()=>worker.Work.Act(work.WorkId,outcome,Action(work),ct))).Code);
                Assert.Equal("INVALID_MAINTENANCE_PHOTOS",(await Assert.ThrowsAsync<ApiException>(()=>worker.Work.Act(work.WorkId,outcome,Action(work,photos:[long.MaxValue]),ct))).Code);
                var data=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAQSURBVChTY/jPwPCfYRQAAAobAf8DJmCxAAAAAElFTkSuQmCC");
                using var stream=new MemoryStream(data);var upload=Json(await worker.Evidence.Upload(staff.Shift.BranchId,work.WorkId,null,null,new FormFile(stream,0,data.Length,"file","repair.png"){Headers=new HeaderDictionary(),ContentType="image/png"},ct));
                await db.SaveChangesAsync(ct);var photoId=upload.GetProperty("fields").GetProperty("attachmentId").GetInt64();
                var photo=await db.Attachments.SingleAsync(a=>a.AttachmentId==photoId,ct);var path=Path.Combine(root,photo.StorageKey);
                await File.WriteAllBytesAsync(path,[1,2,3],ct);
                Assert.Equal("EVIDENCE_INTEGRITY_FAILURE",(await Assert.ThrowsAsync<ApiException>(()=>worker.Work.Act(work.WorkId,outcome,Action(work,photos:[photoId]),ct))).Code);
                await File.WriteAllBytesAsync(path,data,ct);
                if(outcome=="unrepairable")
                    Assert.Equal("REASON_REQUIRES_10_TO_2000_CHARACTERS",(await Assert.ThrowsAsync<ApiException>(()=>worker.Work.Act(work.WorkId,outcome,Action(work,reason:null,photos:[photoId]),ct))).Code);
                var report=Action(work,reason:outcome=="complete"?null:"Cannot repair",photos:[photoId]);
                await worker.Work.Act(work.WorkId,outcome,report,ct);
                await worker.Work.Act(work.WorkId,outcome,report,ct);
                Assert.False(await db.WorkIntervals.AnyAsync(t=>t.WorkId==work.WorkId&&t.ClosedAt==null,ct));
                Assert.Equal("COMPLETED",work.Status);Assert.Equal(outcome=="complete"?"PASS":"FAIL",work.Result);
                Assert.False(await db.WorkAssignments.AnyAsync(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE",ct));
                if(outcome=="complete")Assert.False(await db.WorkEvents.AnyAsync(e=>e.WorkId==work.WorkId&&e.EventType=="REPLACEMENT_CONFIRMED",ct));
                else{
                    await supervisor.Work.ReplaceAsset(work.WorkId,"propose",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Confirmed irreparable","Buy equivalent replacement"),ct);
                    Assert.True(await db.WorkEvents.AnyAsync(e=>e.WorkId==work.WorkId&&e.EventType=="REPLACEMENT_CONFIRMED",ct));
                    Assert.Contains(Json(await manager.Work.ReplacementQueue(staff.Shift.BranchId,ct)).EnumerateArray(),x=>x.GetProperty("workId").GetInt64()==work.WorkId&&x.GetProperty("canDecide").GetBoolean());
                    await manager.Work.ReplaceAsset(work.WorkId,"approve",new(Convert.ToBase64String(work.RowVersion),Guid.NewGuid(),"Approve replacement procurement"),ct);
                    Assert.False(await db.WorkLinks.AnyAsync(l=>l.TargetWorkId==work.WorkId&&l.Relation=="SOURCE",ct));
                }
            }
            await tx.RollbackAsync(ct);db.ChangeTracker.Clear();Assert.Equal(initial,await db.WorkItems.CountAsync(ct));
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}



