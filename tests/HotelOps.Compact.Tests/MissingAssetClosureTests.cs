using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
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
[Collection("Note2511 SQL")]
public sealed class MissingAssetClosureTests
{
    private sealed class Clock:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,11,27,8,25,0,TimeSpan.Zero); }
    private sealed class Accessor:IHttpContextAccessor { public HttpContext? HttpContext {get;set;} }
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
    [TaskSupplyDatabaseFact]
    public Task PurchaseRejectionDeliveryReinspectionAndClosureUseActualRegisteredAssets()=>VerifyClosure(false);
    [TaskSupplyDatabaseFact]
    public Task PassedAssetInspectionRetainsExplicitSafetyHold()=>VerifyClosure(true);
    private async Task VerifyClosure(bool safety)
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        var builder=new SqlConnectionStringBuilder(connection);Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",builder.InitialCatalog);Assert.True(builder.IntegratedSecurity);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        var initialMovements=await db.ReusableMovements.CountAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.TaskSupplyTestFixture',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture unavailable',1;",ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture unavailable',1;",ct);
        await (Task)typeof(StaffSupplyService).GetMethod("Lock",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[db,ct])!;
        var clock=new Clock();var now=clock.GetUtcNow().UtcDateTime;
        var day=await(from d in db.WorkdayAssignments join s in db.WorkdayShifts on d.ShiftId equals s.ShiftId join r in db.Roles on s.RoleId equals r.RoleId
            where d.WorkDate==new DateOnly(2026,11,27)&&d.Status=="WORKING"&&r.Code=="HK_STAFF" orderby d.UserId select d).FirstAsync(ct);
        var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AssignmentId==day.AssignmentId,ct);var branch=allocation.BranchId;
        async Task<(WorkService Work,StaffSupplyService Supplies)> Services(long id){
            var user=await db.Users.SingleAsync(u=>u.UserId==id,ct);var session=new UserSession{UserId=id,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=user.SecurityStamp,CreatedAt=now,ExpiresAt=now.AddHours(12)};
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
            var actor=new Actor(new Accessor{HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString()),new Claim("session_id",session.SessionId.ToString())],"fixture"))}});
            var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);var notifications=new NotificationService(db,actor,access,atomic,clock);
            var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
            return(new(db,access,actor,atomic,audit,clock,notifications,documents,new(db,actor,clock),supplies,new(db,access,actor,atomic,audit,clock,supplies,documents,notifications)),supplies);
        }
        var staff=await Services(day.UserId);
        var supervisorId=await(from d in db.WorkdayAssignments join s in db.WorkdayShifts on d.ShiftId equals s.ShiftId join r in db.Roles on s.RoleId equals r.RoleId
            where s.BranchId==branch&&d.WorkDate==day.WorkDate&&d.Status=="WORKING"&&r.Code=="HOUSEKEEPING_SUPERVISOR" select d.UserId).FirstAsync(ct);
        var supervisor=await Services(supervisorId);
        var managerId=await(from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId where ur.BranchId==branch&&ur.RevokedAt==null&&r.Code=="OPERATION_MANAGER"&&db.Users.Any(u=>u.UserId==ur.UserId&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null) select ur.UserId).FirstAsync(ct);
        var manager=await Services(managerId);
        var room=await db.Rooms.FirstAsync(r=>r.BranchId==branch&&r.IsActive&&db.Assets.Any(a=>a.RoomId==r.RoomId&&a.IsActive)
            &&!db.WorkItems.Any(w=>w.RoomId==r.RoomId&&(w.Kind=="CLEANING"||w.Kind=="REWORK")&&w.Status!="COMPLETED"&&w.Status!="CLOSED"&&w.Status!="CANCELLED"),ct);
        var type=await db.ReferenceValues.SingleAsync(r=>r.Code==$"ROOM_ISSUE_{branch}_MISSING",ct);
        async Task<string> RoomVersion()=>Convert.ToBase64String(await db.Rooms.AsNoTracking().Where(r=>r.RoomId==room.RoomId).Select(r=>r.RowVersion).SingleAsync(ct));
        var cleaning=new WorkItem{BranchId=branch,RoomId=room.RoomId,TurnoverNo=room.TurnoverNo,Kind="CLEANING",Status="IN_PROGRESS",WorkNo=Guid.NewGuid().ToString("N"),Title="Rollback asset closure",CreatedAt=now,CreatedBy=day.UserId,ActualStartAt=now};
        db.WorkItems.Add(cleaning);await db.SaveChangesAsync(ct);
        db.WorkAssignments.Add(new(){BranchId=branch,WorkId=cleaning.WorkId,AssigneeUserId=day.UserId,AssignedBy=supervisorId,AssignedAt=now,AcceptedAt=now,AssignmentStatus="ACTIVE"});await db.SaveChangesAsync(ct);
        var missing=await db.Assets.FirstAsync(a=>a.RoomId==room.RoomId&&a.BranchId==branch&&a.IsActive&&a.AssetTypeId!=null,ct);
        var result=Json(await staff.Work.CreateRoomIssueBatch(new(branch,room.RoomId,type.ReferenceId,await RoomVersion(),Guid.NewGuid(),[new(null,missing.AssetId)],Impact:safety?"SAFETY":"READINESS",CleaningWorkId:cleaning.WorkId),ct));
        var id=result.GetProperty("workIds")[0].GetInt64();
        async Task<RoomIssueActionRequest> Request(long? replacement=null)=>new(Convert.ToBase64String(await db.WorkItems.Where(w=>w.WorkId==id).Select(w=>w.RowVersion).SingleAsync(ct)),Guid.NewGuid(),"Actual independent asset closure verification",RoomRowVersion:await RoomVersion(),ReplacementAssetId:replacement);
        await supervisor.Work.ActRoomIssue(id,"approve",await Request(),ct);
        await manager.Work.ActRoomIssue(id,"reject-purchase",await Request(),ct);
        Assert.True(Json(await supervisor.Work.RoomIssueDetail(id,ct)).GetProperty("canVerifyMissingAsset").GetBoolean());
        await supervisor.Work.ActRoomIssue(id,"approve",await Request(),ct);
        await manager.Work.ActRoomIssue(id,"approve-purchase",await Request(),ct);
        Assert.True(Json(await manager.Work.RoomIssueDetail(id,ct)).GetProperty("canRecordMissingAssetDelivery").GetBoolean());
        var replacement=new Asset{BranchId=branch,AssetCode=Guid.NewGuid().ToString("N"),Name="Rollback registered replacement",AssetTypeId=missing.AssetTypeId,AssetTypeKind=missing.AssetTypeKind,RoomId=room.RoomId,ConditionCode="GOOD",IsActive=true,CreatedAt=now};
        db.Assets.Add(replacement);await db.SaveChangesAsync(ct);
        Assert.Equal("MISSING_ASSET_STILL_ASSIGNED_TO_ROOM",(await Assert.ThrowsAsync<ApiException>(()=>Act("record-delivery",manager.Work,replacement.AssetId))).Code);
        missing.RoomId=null;await db.SaveChangesAsync(ct);
        Assert.Equal("INVALID_REPLACEMENT_ASSET",(await Assert.ThrowsAsync<ApiException>(()=>Act("record-delivery",manager.Work,missing.AssetId))).Code);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiException>(()=>Act("record-delivery",staff.Work,replacement.AssetId))).Status);
        replacement.ConditionCode="BROKEN";await db.SaveChangesAsync(ct);
        Assert.Equal("INVALID_REPLACEMENT_ASSET",(await Assert.ThrowsAsync<ApiException>(()=>Act("record-delivery",manager.Work,replacement.AssetId))).Code);
        replacement.ConditionCode="GOOD";await db.SaveChangesAsync(ct);
        var delivery=await Request(replacement.AssetId);
        await manager.Work.ActRoomIssue(id,"record-delivery",delivery,ct);
        var eventCount=await db.WorkEvents.CountAsync(e=>e.WorkId==id,ct);
        await manager.Work.ActRoomIssue(id,"record-delivery",delivery,ct);
        Assert.Equal(eventCount,await db.WorkEvents.CountAsync(e=>e.WorkId==id,ct));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED",(await Assert.ThrowsAsync<ApiException>(()=>manager.Work.ActRoomIssue(id,"record-delivery",delivery with{ReplacementAssetId=missing.AssetId},ct))).Code);
        Assert.True(await db.RoomBlocks.AnyAsync(b=>b.SourceWorkId==id&&b.ReleasedAt==null,ct));
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiException>(()=>Act("inspection-pass",manager.Work))).Status);
        var detail=Json(await supervisor.Work.RoomIssueDetail(id,ct));Assert.True(detail.GetProperty("canInspectMissingAssetDelivery").GetBoolean());Assert.Equal(replacement.AssetId,detail.GetProperty("replacementAssetId").GetInt64());
        replacement.RoomId=null;await db.SaveChangesAsync(ct);
        Assert.Equal("INVALID_REPLACEMENT_ASSET",(await Assert.ThrowsAsync<ApiException>(()=>Act("inspection-pass",supervisor.Work))).Code);
        await Act("inspection-fail",supervisor.Work);
        Assert.True(await db.RoomBlocks.AnyAsync(b=>b.SourceWorkId==id&&b.ReleasedAt==null,ct));
        Assert.True(Json(await manager.Work.RoomIssueDetail(id,ct)).GetProperty("canRecordMissingAssetDelivery").GetBoolean());
        replacement.RoomId=room.RoomId;await db.SaveChangesAsync(ct);
        await Act("record-delivery",manager.Work,replacement.AssetId);
        await Act("inspection-pass",supervisor.Work);
        var closed=await db.WorkItems.SingleAsync(w=>w.WorkId==id,ct);Assert.Equal("CLOSED",closed.Status);Assert.Equal(supervisorId,closed.VerifiedBy);
        Assert.Equal(safety,await db.RoomBlocks.AnyAsync(b=>b.SourceWorkId==id&&b.ReleasedAt==null,ct));
        var other=new WorkItem{WorkId=-1,AssetId=missing.AssetId,BranchId=branch,RoomId=room.RoomId};
        var validation=(Task)typeof(WorkService).GetMethod("ValidateMissingReplacement",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(manager.Work,[other,replacement.AssetId,ct])!;
        Assert.Equal("REPLACEMENT_ASSET_ALREADY_USED",(await Assert.ThrowsAsync<ApiException>(()=>validation)).Code);
        Assert.False(await db.WorkItems.AnyAsync(w=>w.ParentWorkId==id&&w.Kind=="MAINTENANCE",ct));
        var queue=Json(await manager.Work.MissingAssetPurchases(branch,ct)).EnumerateArray().Single(x=>x.GetProperty("workId").GetInt64()==id);
        Assert.Equal(day.UserId,queue.GetProperty("createdBy").GetInt64());Assert.Equal("ROOM_ISSUE_INSPECTION_PASSED",queue.GetProperty("status").GetString());Assert.NotEqual(JsonValueKind.Null,queue.GetProperty("updatedAt").ValueKind);
        await tx.RollbackAsync(ct);db.ChangeTracker.Clear();Assert.False(await db.WorkItems.AnyAsync(w=>w.WorkId==id,ct));
        async Task<object> Act(string action,WorkService service,long? replacementId=null)=>await service.ActRoomIssue(id,action,await Request(replacementId),ct);
    }
}
