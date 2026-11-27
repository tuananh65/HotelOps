using System.Reflection;
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
public sealed class AutomaticReusableReturnDatabaseTests
{
    [AutomaticShiftDatabaseFact]
    public async Task ReturnsCleanDirtyDamagedAndPendingApprovalWithoutSweepingNewerCustody()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_AUTO_SHIFT_TEST")!;
        var builder=new SqlConnectionStringBuilder(connection);
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",builder.InitialCatalog);
        Assert.True(builder.IntegratedSecurity);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));var ct=timeout.Token;
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture workday lock unavailable',1;",ct);
        await (Task)typeof(StaffSupplyService).GetMethod("Lock",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[db,ct])!;
        var allocation=await db.StaffSupplyAllocations.OrderByDescending(a=>a.AllocationId).FirstAsync(ct);
        var author=await db.WorkdayAssignments.Where(d=>d.AssignmentId==allocation.AssignmentId).Select(d=>d.CreatedBy).SingleAsync(ct);
        var ready=await db.ReusablePositions.Where(p=>p.BranchId==allocation.BranchId&&p.Status=="READY"&&p.WarehouseId!=null&&p.Quantity>=4).FirstAsync(ct);
        var originalReady=ready.Quantity;
        var clock=TimeProvider.System;
        var type=typeof(StaffSupplyService).Assembly.GetType("HotelOps.Api.Compact.Services.ReusableAccounting")!;
        var accounting=Activator.CreateInstance(type,db,clock)!;
        async Task<ReusablePosition> Position(string status,long? warehouse,long? user)=>await (Task<ReusablePosition>)type.GetMethod("Position",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(accounting,[allocation.BranchId,ready.ItemId,status,warehouse,user,null,ct])!;
        async Task Move(ReusablePosition source,ReusablePosition destination,decimal quantity)=>await (Task)type.GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(accounting,[source,destination,quantity,author,Guid.NewGuid(),"HANDOVER","Rollback automatic return verification",ct,null,null,true])!;
        var clean=await Position("STAFF_HOLDING",null,allocation.UserId);
        var dirty=await Position("DIRTY",null,allocation.UserId);
        var damaged=await Position("DAMAGED",null,allocation.UserId);
        await Move(ready,clean,2);await Move(clean,dirty,1);await Move(ready,damaged,2);
        var proposal=new ReusableWriteOff{BranchId=allocation.BranchId,PositionId=damaged.PositionId,Quantity=1,Reason="Rollback pending write-off",CreatedBy=author,CreatedAt=clock.GetUtcNow().UtcDateTime,OperationKey=Guid.NewGuid()};
        db.ReusableWriteOffs.Add(proposal);await db.SaveChangesAsync(ct);
        var proposalVersion=proposal.RowVersion.ToArray();
        var warehouseDirty=await Position("DIRTY",ready.WarehouseId,null);
        var warehouseDamaged=await Position("DAMAGED",ready.WarehouseId,null);
        var dirtyBefore=warehouseDirty.Quantity;var damagedBefore=warehouseDamaged.Quantity;
        var cleanExpected=clean.Quantity;var dirtyExpected=dirty.Quantity;var damagedExpected=damaged.Quantity;
        var actor=new Actor(new HttpContextAccessor());var atomic=new Atomic(db);var access=new Access(db,actor,clock);var audit=new Audit(db,actor,clock);
        var notifications=new NotificationService(db,actor,access,atomic,clock);
        var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
        var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var method=typeof(StaffSupplyService).GetMethod("ReturnReusable",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Task Return(StaffSupplyAllocation row)=>(Task)method.Invoke(supplies,[row,Guid.NewGuid(),ct,(long?)author])!;
        // A historical allocation must not sweep any pooled units owned by the latest shift.
        var historical=new StaffSupplyAllocation{AllocationId=allocation.AllocationId-1,BranchId=allocation.BranchId,UserId=allocation.UserId,AssignmentId=allocation.AssignmentId};
        var movementCount=await db.ReusableMovements.CountAsync(ct);
        await Return(historical);
        Assert.Equal(movementCount,await db.ReusableMovements.CountAsync(ct));
        Assert.Equal(cleanExpected,clean.Quantity);Assert.Equal(damagedExpected,damaged.Quantity);
        await Return(allocation);await db.SaveChangesAsync(ct);
        Assert.Equal(0,clean.Quantity);Assert.Equal(0,dirty.Quantity);Assert.Equal(0,damaged.Quantity);
        Assert.Equal(originalReady-4+cleanExpected,ready.Quantity);
        Assert.Equal(dirtyBefore+dirtyExpected,warehouseDirty.Quantity);
        Assert.Equal(damagedBefore+damagedExpected,warehouseDamaged.Quantity);
        Assert.Equal(warehouseDamaged.PositionId,proposal.PositionId);
        Assert.Equal("PENDING",proposal.Status);Assert.Null(proposal.DecidedBy);Assert.Null(proposal.DecidedAt);
        Assert.Equal(1,proposal.Quantity);Assert.Equal(author,proposal.CreatedBy);Assert.NotEqual(proposalVersion,proposal.RowVersion);
        // Pending quantity is still encumbered at its new location.
        var held=await db.ReusableWriteOffs.Where(p=>p.PositionId==warehouseDamaged.PositionId&&p.Status=="PENDING").SumAsync(p=>p.Quantity,ct);
        Assert.True(held>=1);Assert.True(held<=warehouseDamaged.Quantity);
        movementCount=await db.ReusableMovements.CountAsync(ct);
        var stockCount=await db.StockMovements.CountAsync(ct);
        await Return(allocation);await db.SaveChangesAsync(ct);
        Assert.Equal(movementCount,await db.ReusableMovements.CountAsync(ct));Assert.Equal(stockCount,await db.StockMovements.CountAsync(ct));
        await transaction.RollbackAsync(ct);db.ChangeTracker.Clear();
        Assert.False(await db.ReusableWriteOffs.AnyAsync(p=>p.WriteOffId==proposal.WriteOffId,ct));
        Assert.Equal(originalReady,await db.ReusablePositions.Where(p=>p.PositionId==ready.PositionId).Select(p=>p.Quantity).SingleAsync(ct));
    }
}
