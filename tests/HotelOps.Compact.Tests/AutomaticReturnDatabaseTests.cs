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
public sealed class AutomaticReturnDatabaseTests
{
    private sealed class ReturnClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [AutomaticShiftDatabaseFact]
    public async Task EndedShiftReturnsCanonicalRemainderAndRetryPostsNothing()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_AUTO_SHIFT_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",new SqlConnectionStringBuilder(connection).InitialCatalog);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));var ct=timeout.Token;
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture workday lock unavailable',1;",ct);
        var allocations=await db.StaffSupplyAllocations.Where(a=>a.Status!="CLOSED"&&db.WorkdayAssignments.Any(d=>d.AssignmentId==a.AssignmentId&&d.Status=="ENDED"&&d.StartedAt!=null&&d.EndedAt!=null)).ToArrayAsync(ct);
        Assert.NotEmpty(allocations);
        var ids=allocations.Select(a=>a.AllocationId).ToArray();
        var balances=await db.StaffSupplyBalances.Where(b=>ids.Contains(b.AllocationId)&&db.ItemLifecycleProfiles.Any(p=>p.ItemId==b.ItemId&&p.Classification=="CONSUMABLE"&&db.StaffSupplyAllocations.Any(a=>a.AllocationId==b.AllocationId&&a.BranchId==p.BranchId))).ToArrayAsync(ct);
        Assert.Contains(balances,b=>SupplyMath.Remaining(b)>0);
        var expected=balances.ToDictionary(b=>b.BalanceId,b=>(Used:b.Used,Returned:b.Returned,Remaining:SupplyMath.Remaining(b)));
        var actor=new Actor(new HttpContextAccessor());var clock=new ReturnClock();
        var atomic=new Atomic(db);var access=new Access(db,actor,clock);var audit=new Audit(db,actor,clock);
        var notifications=new NotificationService(db,actor,access,atomic,clock);
        var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
        var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var close=typeof(StaffSupplyService).GetMethod("CloseEndedAllocations",BindingFlags.NonPublic|BindingFlags.Instance)!;
        await (Task)close.Invoke(supplies,[null,ct])!;
        Assert.All(allocations,a=>Assert.NotEqual("CLOSED",a.Status));
        Assert.All(balances,b=>Assert.Equal(expected[b.BalanceId].Returned,b.Returned));
        clock.Now=clock.Now.AddMinutes(5).AddTicks(-1);
        await (Task)close.Invoke(supplies,[null,ct])!;
        Assert.All(allocations,a=>Assert.NotEqual("CLOSED",a.Status));
        clock.Now=clock.Now.AddTicks(1);
        await (Task)close.Invoke(supplies,[null,ct])!;
        Assert.All(allocations,a=>Assert.Equal("CLOSED",a.Status));
        foreach(var balance in balances){
            Assert.Equal(0,SupplyMath.Remaining(balance));Assert.Equal(0,balance.Reserved);
            Assert.Equal(expected[balance.BalanceId].Used,balance.Used);
            Assert.Equal(expected[balance.BalanceId].Returned+expected[balance.BalanceId].Remaining,balance.Returned);
        }
        var balanceIds=balances.Select(b=>b.BalanceId).ToArray();
        Assert.False(await db.SupplyReservations.AnyAsync(r=>balanceIds.Contains(r.BalanceId)&&r.Status=="RESERVED",ct));
        var documentsCount=await db.SupplyStockDocuments.CountAsync(ct);
        var movementCount=await db.StockMovements.CountAsync(ct);
        var ledgerCount=await db.StaffSupplyLedgers.CountAsync(ct);
        await (Task)close.Invoke(supplies,[null,ct])!;
        Assert.Equal(documentsCount,await db.SupplyStockDocuments.CountAsync(ct));
        Assert.Equal(movementCount,await db.StockMovements.CountAsync(ct));
        Assert.Equal(ledgerCount,await db.StaffSupplyLedgers.CountAsync(ct));
        await transaction.RollbackAsync(ct);
        db.ChangeTracker.Clear();
        Assert.Equal(allocations.Length,await db.StaffSupplyAllocations.CountAsync(a=>ids.Contains(a.AllocationId)&&a.Status!="CLOSED",ct));
    }
}
