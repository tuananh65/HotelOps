using System.Security.Claims;
using System.Security.Cryptography;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Compact.Tests;

[Collection("Note2511 SQL")]
public sealed class ReplenishmentReceiptDatabaseTests
{
    private sealed class Context:IHttpContextAccessor { public HttpContext? HttpContext {get;set;} }
    private sealed class Clock:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,11,27,3,0,0,TimeSpan.Zero); }
    [TaskSupplyDatabaseFact]
    public async Task ApprovedMixedReplenishmentReceivesOnceWithoutClassifyingAssetsAsConsumables()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",new SqlConnectionStringBuilder(connection).InitialCatalog);
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=limit.Token;
        await using var tx=await db.Database.BeginTransactionAsync(ct);var clock=new Clock();var now=clock.GetUtcNow().UtcDateTime;
        var shift=await (from a in db.WorkdayAssignments join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId join r in db.Roles on s.RoleId equals r.RoleId
            where a.Status=="WORKING"&&a.WorkDate==new DateOnly(2026,11,27)&&r.Code=="STOREKEEPER" select new{a.UserId,s.BranchId}).FirstAsync(ct);
        async Task<RequestWorkflowService> Service(long userId){
            var user=await db.Users.SingleAsync(u=>u.UserId==userId,ct);var session=new UserSession{UserId=userId,TokenHash=RandomNumberGenerator.GetBytes(32),SecurityStamp=user.SecurityStamp,CreatedAt=now,ExpiresAt=now.AddHours(2)};
            db.UserSessions.Add(session);await db.SaveChangesAsync(ct);
            var http=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,userId.ToString()),new Claim("session_id",session.SessionId.ToString())],"fixture"))};
            http.Request.Method="POST";http.Request.Path="/api/v2/requests";
            var actor=new Actor(new Context{HttpContext=http});var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);var notifications=new NotificationService(db,actor,access,atomic,clock);
            var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
            if(await access.HasRole("OPERATION_MANAGER",shift.BranchId,ct)){
                var other=await db.Warehouses.FirstAsync(w=>w.IsActive&&w.BranchId!=shift.BranchId,ct);
                var own=await db.Warehouses.FirstAsync(w=>w.IsActive&&w.BranchId==shift.BranchId,ct);
                Assert.False(await ShiftCommandAccess.CanOperate(db,userId,other.BranchId,now,ct));
                http.Request.Path="/api/v2/documents";
                var input=new CreateDocumentRequest(other.BranchId,"TRANSFER_REQUEST",null,other.WarehouseId,own.WarehouseId,null,null,null,"Yêu cầu điều chuyển kiểm thử",null,DestinationBranchId:shift.BranchId);
                var actionContext=new ActionContext(http,new RouteData(),new ActionDescriptor());
                var executing=new ActionExecutingContext(actionContext,new List<IFilterMetadata>(),new Dictionary<string,object?>{{"request",input}},new object());
                var called=false;
                await new ShiftCommandFilter(db,actor,clock,new ShiftExpiryService(db,atomic,clock,supplies)).OnActionExecutionAsync(executing,()=>{called=true;return Task.FromResult(new ActionExecutedContext(actionContext,new List<IFilterMetadata>(),new object()));});
                Assert.True(called);
                var created=(ResourceDto)await documents.Create(input,ct);
                var document=await db.InventoryDocuments.SingleAsync(d=>d.DocumentId==Convert.ToInt64(created.Fields["documentId"]),ct);
                http.Request.Path=$"/api/v2/documents/{document.DocumentId}/lines";
                executing.ActionArguments.Clear();executing.ActionArguments["id"]=document.DocumentId;called=false;
                await new ShiftCommandFilter(db,actor,clock,new ShiftExpiryService(db,atomic,clock,supplies)).OnActionExecutionAsync(executing,()=>{called=true;return Task.FromResult(new ActionExecutedContext(actionContext,new List<IFilterMetadata>(),new object()));});
                Assert.True(called);http.Request.Path="/api/v2/requests";
            }
            return new(db,access,actor,atomic,audit,clock,supplies,documents,notifications);
        }
        var managerId=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId where ur.BranchId==shift.BranchId&&ur.RevokedAt==null&&r.Code=="OPERATION_MANAGER"&&ur.AssignedAt<=now&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&db.Users.Any(u=>u.UserId==ur.UserId&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null) select ur.UserId).FirstAsync(ct);
        Assert.True(await ShiftCommandAccess.CanOperate(db,managerId,shift.BranchId,now,ct));
        var warehouse=await db.Warehouses.FirstAsync(w=>w.BranchId==shift.BranchId&&w.IsActive,ct);
        var items=new List<SupplyIssueQuantity>();
        foreach(var classification in new[]{"ASSET","REUSABLE","CONSUMABLE"})items.Add(new(await db.ItemLifecycleProfiles.Where(p=>p.BranchId==shift.BranchId&&p.Classification==classification&&db.InventoryItems.Any(i=>i.ItemId==p.ItemId&&i.IsActive)).Select(p=>p.ItemId).FirstAsync(ct),2));
        var keeper=await Service(shift.UserId);var manager=await Service(managerId);
        var created=await keeper.CreateReplenishment(new(shift.BranchId,warehouse.WarehouseId,items,"Bổ sung hàng kiểm thử",Guid.NewGuid()),ct);
        var approved=await manager.Act(created.Request.WorkId,"approve",new(created.Request.RowVersion,Guid.NewGuid(),ApprovedItems:items),ct);
        Assert.Equal("PENDING_RECEIPT",approved.Request.Status);
        var partial=new ReceiveReplenishmentWorkflowRequest(approved.Request.RowVersion,Guid.NewGuid(),items.Select(i=>i with{Quantity=1}).ToArray(),"Nhà cung cấp kiểm thử");
        var received=await keeper.Receive(created.Request.WorkId,partial,ct);
        Assert.Equal("PENDING_RECEIPT",received.Request.Status);
        var count=await db.StockMovements.CountAsync(ct);await keeper.Receive(created.Request.WorkId,partial,ct);Assert.Equal(count,await db.StockMovements.CountAsync(ct));
        var full=await keeper.Receive(created.Request.WorkId,new(received.Request.RowVersion,Guid.NewGuid(),items.Select(i=>i with{Quantity=1}).ToArray(),"Nhà cung cấp kiểm thử"),ct);
        Assert.Equal("FULFILLED",full.Request.Status);Assert.All(full.Items,i=>Assert.Equal(2,i.ReceivedQuantity));
        await tx.RollbackAsync(ct);
    }
}
