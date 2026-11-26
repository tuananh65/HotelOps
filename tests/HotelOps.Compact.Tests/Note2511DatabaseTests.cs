using System.Security.Claims;
using System.Reflection;
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

public sealed class Note2511DatabaseFactAttribute:FactAttribute
{
    public Note2511DatabaseFactAttribute(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOTELOPS_NOTE2511_TEST")))Skip="Requires the backed-up isolated Note2511 clone.";}
}
[Collection("Note2511 SQL")]
public sealed class Note2511DatabaseTests
{
    // A fixture accessor must not lose HttpContext when the asynchronous factory returns.
    private sealed class FixtureAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
    [Note2511DatabaseFact]
    public async Task ConsumableRefillRequiresOperationsApprovalAndIssuesExactlyOnce()
    {
        await using var db=Open();using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var (hk,shift,now)=await Prepare(db,"HK_STAFF",ct);
        var (store,storeShift,storeNow)=await Prepare(db,"STOREKEEPER",ct);
        Assert.Equal(shift.BranchId,storeShift.BranchId);
        // Keep both operational actors on the same simulated interval, regardless of local seed shift ordering.
        storeShift.StartMinute=shift.StartMinute;storeShift.EndMinute=shift.EndMinute;
        hk.Status="WORKING";hk.StartedAt=now;store.Status="WORKING";store.StartedAt=now;
        await db.SaveChangesAsync(ct);
        async Task<(StaffSupplyService Supplies,RequestWorkflowService Requests)> Services(long userId)
        {
            var user=await db.Users.SingleAsync(u=>u.UserId==userId,ct);
            var session=new UserSession{UserId=userId,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=user.SecurityStamp,CreatedAt=now,ExpiresAt=now.AddHours(2)};
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
            var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,userId.ToString()),new Claim("session_id",session.SessionId.ToString())],"fixture"))};
            context.Request.Method="POST";context.Request.Path="/api/v2/request-workflows";
            var actor=new Actor(new FixtureAccessor{HttpContext=context});var clock=new Clock(now);var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
            var notifications=new NotificationService(db,actor,access,atomic,clock);var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
            var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
            return(supplies,new RequestWorkflowService(db,access,actor,atomic,audit,clock,supplies,documents,notifications));
        }
        var staff=await Services(hk.UserId);
        await (Task)typeof(StaffSupplyService).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(staff.Supplies,[hk.AssignmentId,ct])!;
        var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AssignmentId==hk.AssignmentId,ct);
        var balance=await db.StaffSupplyBalances.FirstAsync(b=>b.AllocationId==allocation.AllocationId&&db.ItemLifecycleProfiles.Any(p=>p.BranchId==shift.BranchId&&p.ItemId==b.ItemId&&p.Classification=="CONSUMABLE"),ct);
        var before=balance.AdditionalIssued;var movements=await db.StockMovements.CountAsync(ct);
        var created=await staff.Requests.CreateRefill(new(allocation.AllocationId,[new(balance.ItemId,1)],"Isolated SQL refill verification",Guid.NewGuid()),ct);
        Assert.Equal("PENDING_OPERATIONS_MANAGER",created.Request.Status);Assert.Equal("OPERATION_MANAGER",created.Request.QueueRole);
        Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
        var managerId=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId join u in db.Users on ur.UserId equals u.UserId where r.Code=="OPERATION_MANAGER"&&ur.BranchId==shift.BranchId&&ur.RevokedAt==null&&u.AccountStatus=="ACTIVE" select u.UserId).FirstAsync(ct);
        var manager=await Services(managerId);
        var approved=await manager.Requests.Act(created.Request.WorkId,"approve",new(created.Request.RowVersion,Guid.NewGuid()),ct);
        Assert.Equal("PENDING_STOREKEEPER",approved.Request.Status);Assert.Equal("STOREKEEPER",approved.Request.QueueRole);
        Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
        var keeper=await Services(store.UserId);var request=new RequestWorkflowAction(approved.Request.RowVersion,Guid.NewGuid());
        var issued=await keeper.Requests.Act(created.Request.WorkId,"issue",request,ct);
        Assert.Equal("FULFILLED",issued.Request.Status);Assert.Equal(before+1,balance.AdditionalIssued);
        var after=await db.StockMovements.CountAsync(ct);Assert.True(after>movements);
        await keeper.Requests.Act(created.Request.WorkId,"issue",request,ct);
        Assert.Equal(after,await db.StockMovements.CountAsync(ct));Assert.Equal(before+1,balance.AdditionalIssued);
        await tx.RollbackAsync(ct);
    }
    private sealed class Clock(DateTime instant):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(instant,TimeSpan.Zero); }
    private static HotelOpsCompactDbContext Open()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_NOTE2511_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNote2511260145",new SqlConnectionStringBuilder(connection).InitialCatalog);
        return new(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
    }
    private static async Task<(WorkdayAssignment Day,WorkdayShift Shift,DateTime Now)> Prepare(HotelOpsCompactDbContext db,string code,CancellationToken ct)
    {
        var candidate=await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
            join ur in db.UserRoles on r.RoleId equals ur.RoleId join u in db.Users on ur.UserId equals u.UserId
            where r.Code==code&&r.IsActive&&s.IsActive&&ur.BranchId==s.BranchId&&ur.RevokedAt==null&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
                &&db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==s.BranchId&&m.DepartmentId==s.DepartmentId&&m.Kind=="EMPLOYMENT"&&m.ValidTo==null)
            select new{s.ShiftId,u.UserId}).FirstAsync(ct);
        var shift=await db.WorkdayShifts.SingleAsync(s=>s.ShiftId==candidate.ShiftId,ct);
        var date=new DateOnly(2026,12,1);
        var zone=await db.Branches.Where(b=>b.BranchId==shift.BranchId).Select(b=>b.Timezone).SingleAsync(ct);
        var now=TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.StartMinute+10),TimeZoneInfo.FindSystemTimeZoneById(zone));
        var day=new WorkdayAssignment{ShiftId=shift.ShiftId,UserId=candidate.UserId,WorkDate=date,CreatedBy=candidate.UserId,CreatedAt=now,Status="PLANNED"};
        db.WorkdayAssignments.Add(day);await db.SaveChangesAsync(ct);
        return(day,shift,now);
    }
    [Note2511DatabaseFact]
    public async Task SixOperationalRolesRequireActualCurrentBranchAttendance()
    {
        await using var db=Open();using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=limit.Token;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        foreach(var role in new[]{"HK_STAFF","HOUSEKEEPING_SUPERVISOR","MAINTENANCE_STAFF","MAINTENANCE_SUPERVISOR","STOREKEEPER","RECEPTION"}){
            var (day,shift,now)=await Prepare(db,role,ct);
            Assert.False(await ShiftCommandAccess.CanOperate(db,day.UserId,shift.BranchId,now,ct));
            day.Status="WORKING";day.StartedAt=now;await db.SaveChangesAsync(ct);
            Assert.True(await ShiftCommandAccess.CanOperate(db,day.UserId,shift.BranchId,now,ct));
            var grant=await (from g in db.RolePermissions join p in db.Permissions on g.PermissionId equals p.PermissionId
                where g.RoleId==shift.RoleId&&p.IsActive&&(g.DataScope=="OWN"||g.DataScope=="BRANCH"||g.DataScope=="ALL") select p.Code).FirstAsync(ct);
            Assert.True(await ShiftCommandAccess.CanOperate(db,day.UserId,shift.BranchId,now,ct,grant,true));
            Assert.False(await ShiftCommandAccess.CanOperate(db,day.UserId,shift.BranchId,now,ct,"NOT_GRANTED_TO_SHIFT_ROLE",true));
            var foreign=await db.Branches.Where(b=>b.BranchId!=shift.BranchId&&b.IsActive).Select(b=>b.BranchId).FirstAsync(ct);
            Assert.False(await ShiftCommandAccess.CanOperate(db,day.UserId,foreign,now,ct));
            day.Status="ENDED";day.EndedAt=now.AddMinutes(1);await db.SaveChangesAsync(ct);
            Assert.False(await ShiftCommandAccess.CanOperate(db,day.UserId,shift.BranchId,now.AddMinutes(2),ct));
        }
        await tx.RollbackAsync(ct);
    }
    [Note2511DatabaseFact]
    public async Task ReceiptPostsOnceRejectsStaleVersionAndRollsBackFixture()
    {
        await using var db=Open();using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=limit.Token;
        var original=await db.InventoryDocuments.CountAsync(ct);
        await using(var tx=await db.Database.BeginTransactionAsync(ct)){
            var (day,shift,now)=await Prepare(db,"STOREKEEPER",ct);day.Status="WORKING";day.StartedAt=now;
            var user=await db.Users.SingleAsync(u=>u.UserId==day.UserId,ct);
            var session=new UserSession{UserId=user.UserId,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=user.SecurityStamp,CreatedAt=now,ExpiresAt=now.AddHours(2)};
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
            var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.UserId.ToString()),new Claim("session_id",session.SessionId.ToString())],"fixture"))};
            context.Request.Method="POST";context.Request.Path="/api/v2/documents";
            var actor=new Actor(new HttpContextAccessor{HttpContext=context});var clock=new Clock(now);var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
            var notify=new NotificationService(db,actor,access,atomic,clock);var service=new DocumentService(db,access,actor,atomic,audit,clock,notify);
            var warehouse=await db.Warehouses.Where(w=>w.BranchId==shift.BranchId&&w.IsActive).FirstAsync(ct);
            var item=await db.InventoryItems.Where(i=>i.IsActive&&db.ItemLifecycleProfiles.Any(p=>p.ItemId==i.ItemId&&p.BranchId==shift.BranchId&&p.Classification=="CONSUMABLE")).FirstAsync(ct);
            var before=await db.StockMovements.Where(m=>m.WarehouseId==warehouse.WarehouseId&&m.ItemId==item.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
            var created=(ResourceDto)await service.Create(new(shift.BranchId,"RECEIPT",null,null,warehouse.WarehouseId,null,null,"Isolated SQL fixture","Note2511 receipt verification",null,Guid.NewGuid()),ct);
            var id=(long)created.Fields["documentId"]!;var doc=await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==id,ct);
            var stale=Convert.ToBase64String(doc.RowVersion);
            await service.UpdateReceiptDraft(id,new(stale,"NhÃ  cung cáº¥p fixture","Ghi chÃº Ä‘Ã£ chá»‰nh"),ct);
            Assert.Equal("NhÃ  cung cáº¥p fixture",doc.SupplierName);
            stale=Convert.ToBase64String(doc.RowVersion);
            await service.Line(id,null,new(stale,item.ItemId,item.BaseUnitId,3,1,null),ct);
            var error=await Assert.ThrowsAsync<ApiException>(()=>service.Act(id,"post",new(stale,Guid.NewGuid()),ct));Assert.Equal("STALE_ROW_VERSION",error.Code);
            await service.Act(id,"post",new(Convert.ToBase64String(doc.RowVersion),Guid.NewGuid()),ct);
            Assert.Equal("POSTED",doc.Status);
            Assert.Equal(before+3,await db.StockMovements.Where(m=>m.WarehouseId==warehouse.WarehouseId&&m.ItemId==item.ItemId).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0);
            var count=await db.StockMovements.CountAsync(ct);
            await Assert.ThrowsAsync<ApiException>(()=>service.Act(id,"post",new(Convert.ToBase64String(doc.RowVersion),Guid.NewGuid()),ct));
            Assert.Equal(count,await db.StockMovements.CountAsync(ct));
            var stocktake=(ResourceDto)await service.Create(new(shift.BranchId,"STOCKTAKE",null,warehouse.WarehouseId,null,null,null,null,"Fixture kiá»ƒm kÃª",null,Guid.NewGuid()),ct);
            var stocktakeId=(long)stocktake.Fields["documentId"]!;var stocktakeDoc=await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==stocktakeId,ct);
            var other=await db.InventoryItems.Where(i=>i.IsActive&&i.ItemId!=item.ItemId&&db.ItemLifecycleProfiles.Any(p=>p.ItemId==i.ItemId&&p.BranchId==shift.BranchId&&p.Classification=="CONSUMABLE")).FirstAsync(ct);
            await service.AddStocktakeItems(stocktakeId,new(Convert.ToBase64String(stocktakeDoc.RowVersion),[item.ItemId,other.ItemId]),ct);
            Assert.Equal(2,await db.InventoryDocumentLines.CountAsync(l=>l.DocumentId==stocktakeId,ct));
            var counted=await db.InventoryDocumentLines.SingleAsync(l=>l.DocumentId==stocktakeId&&l.ItemId==item.ItemId,ct);
            Assert.Equal(before+3,counted.ExpectedQty);
            await service.Line(stocktakeId,counted.LineId,new(Convert.ToBase64String(stocktakeDoc.RowVersion),item.ItemId,item.BaseUnitId,1,null,null,before+2),ct);
            Assert.Equal(before+3,counted.ExpectedQty);Assert.Equal(before+2,counted.CountedQty);
            Assert.Equal(count,await db.StockMovements.CountAsync(ct));
            await tx.RollbackAsync(ct);
        }
        await using var verify=Open();Assert.Equal(original,await verify.InventoryDocuments.CountAsync(ct));
    }
}
