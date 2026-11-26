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
public sealed class Note2511TransferDatabaseTests
{
    private sealed class Context:IHttpContextAccessor { public HttpContext? HttpContext {get;set;} }
    private sealed class Clock(DateTime instant):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(instant,TimeSpan.Zero); }
    [Note2511DatabaseFact]
    public async Task SourceManagerApprovesAndPartialReceiptKeepsShortageInTransit()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_NOTE2511_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNote2511260145",new SqlConnectionStringBuilder(connection).InitialCatalog);
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(4));var ct=limit.Token;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var warehouses=await db.Warehouses.Where(w=>w.IsActive).OrderBy(w=>w.BranchId).ToArrayAsync(ct);
        var source=warehouses[0];var target=warehouses.First(w=>w.BranchId!=source.BranchId);
        var now=new DateTime(2026,11,27,2,10,0,DateTimeKind.Utc);
        async Task<DocumentService> Service(string role,long branch)
        {
            var user=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId join u in db.Users on ur.UserId equals u.UserId
                where r.Code==role&&r.IsActive&&ur.BranchId==branch&&ur.RevokedAt==null&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
                select u).FirstAsync(ct);
            if(role=="STOREKEEPER"){
                var shift=await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
                    where s.BranchId==branch&&s.IsActive&&r.Code==role&&s.StartMinute<=550&&s.EndMinute>550
                      &&db.Memberships.Any(m=>m.UserId==user.UserId&&m.BranchId==branch&&m.DepartmentId==s.DepartmentId&&m.Kind=="EMPLOYMENT"&&m.ValidTo==null)
                    select s).FirstAsync(ct);
                db.WorkdayAssignments.Add(new WorkdayAssignment{ShiftId=shift.ShiftId,UserId=user.UserId,WorkDate=new(2026,11,27),Status="WORKING",StartedAt=now,CreatedAt=now,CreatedBy=user.UserId});
            }
            var session=new UserSession{UserId=user.UserId,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=user.SecurityStamp,CreatedAt=now,ExpiresAt=now.AddHours(2)};
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
            var http=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.UserId.ToString()),new Claim("session_id",session.SessionId.ToString())],"fixture"))};
            http.Request.Method="POST";http.Request.Path="/api/v2/documents";
            var actor=new Actor(new Context{HttpContext=http});var clock=new Clock(now);var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
            return new(db,access,actor,atomic,audit,clock,new NotificationService(db,actor,access,atomic,clock));
        }
        var sender=await Service("STOREKEEPER",source.BranchId);var receiver=await Service("STOREKEEPER",target.BranchId);
        var sourceManager=await Service("OPERATION_MANAGER",source.BranchId);var destinationManager=await Service("OPERATION_MANAGER",target.BranchId);
        var batch=await db.StockBatches.FirstAsync(b=>b.WarehouseId==source.WarehouseId&&b.Status=="READY"&&b.WarehouseQuantity-b.ReservedQuantity>=4
            &&(b.ExpiresOn==null||b.ExpiresOn>new DateOnly(2026,11,27))
            &&db.ItemLifecycleProfiles.Any(p=>p.BranchId==source.BranchId&&p.ItemId==b.ItemId&&p.Classification=="CONSUMABLE"&&p.ExpiryPolicy!="UNKNOWN"),ct);
        var item=await db.InventoryItems.SingleAsync(i=>i.ItemId==batch.ItemId,ct);
        async Task<decimal> Stock(long warehouse)=>await db.StockMovements.Where(m=>m.WarehouseId==warehouse&&m.ItemId==item.ItemId).SumAsync(m=>m.SignedBaseQty,ct);
        var sourceBefore=await Stock(source.WarehouseId);var destinationBefore=await Stock(target.WarehouseId);
        var resource=(ResourceDto)await destinationManager.Create(new(source.BranchId,"TRANSFER_REQUEST",null,source.WarehouseId,target.WarehouseId,null,null,null,"Isolated transfer verification",null,DestinationBranchId:target.BranchId),ct);
        var root=await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==Convert.ToInt64(resource.Fields["documentId"]),ct);
        await destinationManager.Line(root.DocumentId,null,new(Convert.ToBase64String(root.RowVersion),item.ItemId,item.BaseUnitId,4,null,null),ct);
        ActionRequest Action(InventoryDocument document)=>new(Convert.ToBase64String(document.RowVersion),Guid.NewGuid(),"Isolated transfer verification");
        await destinationManager.Act(root.DocumentId,"submit",Action(root),ct);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiException>(()=>destinationManager.Act(root.DocumentId,"approve",Action(root),ct))).Status);
        await sourceManager.Act(root.DocumentId,"approve",Action(root),ct);
        async Task<InventoryDocument> Derive(DocumentService service,InventoryDocument parent,string kind,decimal quantity){
            var line=await db.InventoryDocumentLines.SingleAsync(l=>l.DocumentId==parent.DocumentId,ct);
            var result=(ResourceDto)await service.Derive(parent.DocumentId,new(Convert.ToBase64String(parent.RowVersion),Guid.NewGuid(),kind,null,"Isolated transfer verification",[new(line.LineId,quantity)]),ct);
            var id=Convert.ToInt64(result.Fields["documentId"]);return await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==id,ct);
        }
        var shipment=await Derive(sender,root,"TRANSFER",4);
        Assert.Equal(403,(await Assert.ThrowsAsync<ApiException>(()=>receiver.Act(shipment.DocumentId,"post",Action(shipment),ct))).Status);
        await sender.Act(shipment.DocumentId,"post",Action(shipment),ct);
        var receipt=await Derive(receiver,shipment,"TRANSFER_RECEIPT",2);var post=Action(receipt);
        await receiver.Act(receipt.DocumentId,"post",post,ct);
        Assert.Equal(sourceBefore-4,await Stock(source.WarehouseId));Assert.Equal(destinationBefore+2,await Stock(target.WarehouseId));
        var movements=await db.StockMovements.CountAsync(ct);
        Assert.Equal(409,(await Assert.ThrowsAsync<ApiException>(()=>receiver.Act(receipt.DocumentId,"post",post,ct))).Status);
        Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
        var detail=JsonSerializer.SerializeToElement(await receiver.Detail(shipment.DocumentId,ct));
        var progress=detail.GetProperty("transfer").GetProperty("progress")[0];
        Assert.Equal(4,progress.GetProperty("shippedQuantity").GetDecimal());Assert.Equal(2,progress.GetProperty("receivedQuantity").GetDecimal());
        Assert.Equal(2,progress.GetProperty("pendingReconciliationQuantity").GetDecimal());
        await tx.RollbackAsync(ct);
    }
}
