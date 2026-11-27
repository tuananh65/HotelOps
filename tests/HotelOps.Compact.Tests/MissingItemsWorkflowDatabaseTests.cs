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
public sealed class MissingItemsWorkflowDatabaseTests
{
    private sealed class Clock:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,11,27,8,25,0,TimeSpan.Zero); }
    private sealed class Accessor:IHttpContextAccessor { public HttpContext? HttpContext {get;set;} }
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
    [TaskSupplyDatabaseFact]
    public async Task ActualServicesPlaceOnceBatchAtomicallyVerifyAndRouteMissingAssets()
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
        var capacity=await db.RoomTypes.Where(t=>t.RoomTypeId==room.RoomTypeId).Select(t=>t.Capacity).SingleAsync(ct);
        var work=new WorkItem{BranchId=branch,RoomId=room.RoomId,TurnoverNo=room.TurnoverNo,Kind="CLEANING",Status="IN_PROGRESS",WorkNo=Guid.NewGuid().ToString("N"),Title="Rollback missing items workflow",CreatedAt=now,CreatedBy=day.UserId,ActualStartAt=now};
        db.WorkItems.Add(work);await db.SaveChangesAsync(ct);db.WorkAssignments.Add(new(){BranchId=branch,WorkId=work.WorkId,AssigneeUserId=day.UserId,AssignedBy=supervisorId,AssignedAt=now,AcceptedAt=now,AssignmentStatus="ACTIVE"});await db.SaveChangesAsync(ct);
        var at=typeof(WorkService).Assembly.GetType("HotelOps.Api.Compact.Services.ReusableAccounting")!;var accounting=Activator.CreateInstance(at,db,clock)!;
        async Task<ReusablePosition> Position(long item,string status,long? warehouse=null,long? user=null,long? roomId=null)=>await (Task<ReusablePosition>)at.GetMethod("Position",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(accounting,[branch,item,status,warehouse,user,roomId,ct])!;
        async Task Move(ReusablePosition source,ReusablePosition destination,decimal quantity,string kind="HANDOVER")=>await (Task)at.GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(accounting,[source,destination,quantity,day.UserId,Guid.NewGuid(),kind,"Rollback real custody transfer",ct,null,null,true])!;
        var configs=await (Task<SupplyConfiguration[]>)typeof(StaffSupplyService).GetMethod("ReusableAllocationSnapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(staff.Supplies,[allocation,ct])!;Assert.True(configs.Length>=2);
        foreach(var c in configs){var ready=await Position(c.ItemId!.Value,"READY",c.WarehouseId);var target=await Position(c.ItemId.Value,"IN_USE",roomId:room.RoomId);await Move(ready,target,capacity*c.PerGuestQuantity);
            var held=await Position(c.ItemId.Value,"STAFF_HOLDING",user:day.UserId);if(held.Quantity<capacity*c.PerGuestQuantity)await Move(ready,held,capacity*c.PerGuestQuantity-held.Quantity);}
        Task Place()=>(Task)typeof(StaffSupplyService).GetMethod("AllocateReusableForTask",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(staff.Supplies,[allocation,work.WorkId,capacity,Guid.NewGuid(),ct])!;
        var shortageConfig=configs[1];
        var shortageHolding=await Position(shortageConfig.ItemId!.Value,"STAFF_HOLDING",user:day.UserId);
        var shortageReady=await Position(shortageConfig.ItemId.Value,"READY",shortageConfig.WarehouseId);
        var restoreQuantity=shortageHolding.Quantity;
        await Move(shortageHolding,shortageReady,restoreQuantity);
        var positionsBeforeShortage=await db.ReusablePositions.AsNoTracking().OrderBy(p=>p.PositionId).Select(p=>new{p.PositionId,p.Quantity}).ToArrayAsync(ct);
        var movesBeforeShortage=await db.ReusableMovements.CountAsync(ct);
        Assert.Equal("STAFF_SUPPLIES_INSUFFICIENT",(await Assert.ThrowsAsync<ApiException>(Place)).Code);
        Assert.Equal(movesBeforeShortage,await db.ReusableMovements.CountAsync(ct));
        Assert.Equal(positionsBeforeShortage,await db.ReusablePositions.AsNoTracking().OrderBy(p=>p.PositionId).Select(p=>new{p.PositionId,p.Quantity}).ToArrayAsync(ct));
        Assert.False(await db.AuditLogs.AnyAsync(a=>a.EntityType=="WorkItem"&&a.EntityKey==work.WorkId.ToString()&&a.Action=="REUSABLE_TASK_PLACED",ct));
        await Move(shortageReady,shortageHolding,restoreQuantity);
        await Place();var count=await db.ReusableMovements.CountAsync(ct);await Place();Assert.Equal(count,await db.ReusableMovements.CountAsync(ct));
        var items=configs.Take(2).Select(c=>c.ItemId!.Value).ToArray();
        async Task<decimal> Clean()=>await db.ReusablePositions.Where(p=>p.BranchId==branch&&items.Contains(p.ItemId)&&(p.Status=="READY"||p.Status=="STAFF_HOLDING"||p.Status=="IN_USE")).SumAsync(p=>p.Quantity,ct);
        var cleanBefore=await Clean();var type=await db.ReferenceValues.SingleAsync(r=>r.Code==$"ROOM_ISSUE_{branch}_MISSING",ct);
        async Task<string> RoomVersion()=>Convert.ToBase64String(await db.Rooms.AsNoTracking().Where(r=>r.RoomId==room.RoomId).Select(r=>r.RowVersion).SingleAsync(ct));
        await db.Entry(room).ReloadAsync(ct);
        var request=new RoomIssueBatchRequest(branch,room.RoomId,type.ReferenceId,await RoomVersion(),Guid.NewGuid(),items.Select(i=>new RoomIssueBatchLine(i,null,1)).ToArray(),CleaningWorkId:work.WorkId);
        var brokenType=await db.ReferenceValues.SingleAsync(r=>r.Code==$"ROOM_ISSUE_{branch}_BROKEN",ct);
        var wrongKind=await Assert.ThrowsAsync<ApiException>(()=>staff.Work.CreateRoomIssueBatch(request with{TypeId=brokenType.ReferenceId,OperationKey=Guid.NewGuid()},ct));
        Assert.Equal("ROOM_ISSUE_BROKEN_ASSET_REQUIRED",wrongKind.Code);
        var reports=await db.WorkEvents.CountAsync(e=>e.EventType=="ROOM_ISSUE_REPORTED",ct);await tx.CreateSavepointAsync("badBatch",ct);
        var invalid=await Assert.ThrowsAsync<ApiException>(()=>staff.Work.CreateRoomIssueBatch(request with{Lines=[new(items[0],null,1),new(null,long.MaxValue)]},ct));Assert.Equal("ROOM_ISSUE_OBJECT_NOT_IN_ROOM",invalid.Code);
        await tx.RollbackToSavepointAsync("badBatch",ct);db.ChangeTracker.Clear();Assert.Equal(reports,await db.WorkEvents.CountAsync(e=>e.EventType=="ROOM_ISSUE_REPORTED",ct));
        request=request with{RoomRowVersion=await RoomVersion()};var created=Json(await staff.Work.CreateRoomIssueBatch(request,ct));var ids=created.GetProperty("workIds").EnumerateArray().Select(e=>e.GetInt64()).ToArray();Assert.Equal(2,ids.Length);
        Assert.Equal(created.GetRawText(),Json(await staff.Work.CreateRoomIssueBatch(request,ct)).GetRawText());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED",(await Assert.ThrowsAsync<ApiException>(()=>staff.Work.CreateRoomIssueBatch(request with{Note="Changed payload"},ct))).Code);
        Assert.Equal("STALE_ROW_VERSION",(await Assert.ThrowsAsync<ApiException>(()=>staff.Work.CreateRoomIssueBatch(request with{OperationKey=Guid.NewGuid()},ct))).Code);
        var dirty=await db.ReusablePositions.Where(p=>p.BranchId==branch&&p.UserId==day.UserId&&p.Status=="DIRTY"&&items.Contains(p.ItemId)).ToArrayAsync(ct);var before=dirty.Sum(p=>p.Quantity);
        Assert.True(await (Task<bool>)typeof(StaffSupplyService).GetMethod("HasPendingMissingCustody",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(staff.Supplies,[day.UserId,ct])!);
        allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AssignmentId==day.AssignmentId,ct);
        await (Task)typeof(StaffSupplyService).GetMethod("ReturnReusable",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(staff.Supplies,[allocation,Guid.NewGuid(),ct,(long?)supervisorId])!;Assert.Equal(before,dirty.Sum(p=>p.Quantity));
        var laundry=await Position(dirty[0].ItemId,"LAUNDRY",configs[0].WarehouseId);Assert.Equal("MISSING_USED_VERIFICATION_PENDING",(await Assert.ThrowsAsync<ApiException>(()=>Move(dirty[0],laundry,1,"LAUNDRY"))).Code);
        foreach(var id in ids){var current=await db.WorkItems.SingleAsync(w=>w.WorkId==id,ct);var action=new RoomIssueActionRequest(Convert.ToBase64String(current.RowVersion),Guid.NewGuid(),"Independent verification of missing used items",RoomRowVersion:await RoomVersion());
            Assert.Equal(403,(await Assert.ThrowsAsync<ApiException>(()=>staff.Work.ActRoomIssue(id,"approve",action,ct))).Status);await supervisor.Work.ActRoomIssue(id,"approve",action,ct);
            count=await db.ReusableMovements.CountAsync(ct);await supervisor.Work.ActRoomIssue(id,"approve",action,ct);Assert.Equal(count,await db.ReusableMovements.CountAsync(ct));Assert.Equal("CLOSED",current.Status);}
        Assert.Equal(before-2,dirty.Sum(p=>p.Quantity));Assert.Equal(cleanBefore,await Clean());
        var warehouse=await Position(dirty[0].ItemId,"DIRTY",configs.Single(c=>c.ItemId==dirty[0].ItemId).WarehouseId);await Move(dirty[0],warehouse,dirty[0].Quantity);
        var returned=Json(await staff.Work.CreateRoomIssueBatch(request with{OperationKey=Guid.NewGuid(),RoomRowVersion=await RoomVersion(),Lines=[new(dirty[0].ItemId,null)]},ct));var returnedId=returned.GetProperty("workIds")[0].GetInt64();
        var warehouseBefore=warehouse.Quantity;var stockBefore=await db.StockMovements.Where(m=>m.WarehouseId==warehouse.WarehouseId&&m.ItemId==warehouse.ItemId).SumAsync(m=>m.SignedBaseQty,ct);
        var returnIssue=await db.WorkItems.SingleAsync(w=>w.WorkId==returnedId,ct);await supervisor.Work.ActRoomIssue(returnedId,"approve",new(Convert.ToBase64String(returnIssue.RowVersion),Guid.NewGuid(),"Independent verification after full warehouse return",RoomRowVersion:await RoomVersion()),ct);
        Assert.Equal(warehouseBefore-1,warehouse.Quantity);Assert.Equal(stockBefore-1,await db.StockMovements.Where(m=>m.WarehouseId==warehouse.WarehouseId&&m.ItemId==warehouse.ItemId).SumAsync(m=>m.SignedBaseQty,ct));
        var asset=await db.Assets.FirstAsync(a=>a.RoomId==room.RoomId&&a.BranchId==branch&&a.IsActive,ct);
        var assetId=Json(await staff.Work.CreateRoomIssueBatch(request with{OperationKey=Guid.NewGuid(),RoomRowVersion=await RoomVersion(),Lines=[new(null,asset.AssetId)]},ct)).GetProperty("workIds")[0].GetInt64();
        var assetIssue=await db.WorkItems.SingleAsync(w=>w.WorkId==assetId,ct);await supervisor.Work.ActRoomIssue(assetId,"approve",new(Convert.ToBase64String(assetIssue.RowVersion),Guid.NewGuid(),"Verified actual missing asset for purchase",RoomRowVersion:await RoomVersion()),ct);
        Assert.Contains(Json(await manager.Work.MissingAssetPurchases(branch,ct)).EnumerateArray(),r=>r.GetProperty("workId").GetInt64()==assetId);await manager.Work.RoomIssueDetail(assetId,ct);
        await manager.Work.ActRoomIssue(assetId,"approve-purchase",new(Convert.ToBase64String(assetIssue.RowVersion),Guid.NewGuid(),"Approve purchase for independently verified missing asset"),ct);
        Assert.False(await db.WorkItems.AnyAsync(w=>w.ParentWorkId==assetId&&w.Kind=="MAINTENANCE",ct));Assert.True(await db.RoomBlocks.AnyAsync(b=>b.SourceWorkId==assetId&&b.ReleasedAt==null,ct));
        Assert.DoesNotContain(await db.ReusablePositions.ToArrayAsync(ct),p=>p.Quantity<0);await tx.RollbackAsync(ct);db.ChangeTracker.Clear();Assert.Equal(initialMovements,await db.ReusableMovements.CountAsync(ct));
    }
}
