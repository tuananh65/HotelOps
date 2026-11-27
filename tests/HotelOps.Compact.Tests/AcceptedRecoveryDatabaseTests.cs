using System.Reflection;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Compact.Tests;

[Collection("Note2511 SQL")]
public sealed class AcceptedRecoveryDatabaseTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTime Now = new(2026,11,27,8,5,0,DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    [AutomaticShiftDatabaseFact]
    public async Task RecoveryIsAtomicPreservesHistoryAndReturnsOnlyUnusedCurrentCustody()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_AUTO_SHIFT_TEST")!;
        var builder=new SqlConnectionStringBuilder(connection);
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",builder.InitialCatalog);
        Assert.True(builder.IntegratedSecurity);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(3));var ct=timeout.Token;
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        foreach(var resource in new[]{"HotelOps.TaskSupplyTestFixture","HotelOps.Workday","HotelOps.StaffSupplies"})
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);
        // These identifiers name the explicitly approved, restored SQL fixture, never production data.
        const long workId=692, historicalWorkId=690, allocationId=9;
        var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AllocationId==allocationId,ct);
        var day=await db.WorkdayAssignments.SingleAsync(a=>a.AssignmentId==allocation.AssignmentId,ct);
        Assert.Equal("WORKING",day.Status);Assert.Equal("OPEN",allocation.Status);
        Assert.True(await db.WorkAssignments.AnyAsync(a=>a.WorkId==workId&&a.AssigneeUserId==allocation.UserId&&a.AssignmentStatus=="ACTIVE"&&a.AcceptedAt!=null,ct));
        Assert.False(await db.StaffSupplyLedgers.AnyAsync(l=>l.WorkId==workId&&l.Kind=="USE",ct));
        var history=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.WorkId==historicalWorkId&&l.Kind=="USE").OrderBy(l=>l.LedgerId).ToArrayAsync(ct);
        Assert.NotEmpty(history);
        var balances=await db.StaffSupplyBalances.Where(b=>b.AllocationId==allocationId).OrderBy(b=>b.BalanceId).ToArrayAsync(ct);
        Assert.True(balances.Length>1);
        var baseline=balances.ToDictionary(b=>b.BalanceId,b=>(b.Used,b.Reserved,b.Returned,b.InitialIssued,b.AdditionalIssued));
        var clock=new Clock();var actor=new Actor(new HttpContextAccessor());var atomic=new Atomic(db);
        var access=new Access(db,actor,clock);var audit=new Audit(db,actor,clock);
        var notifications=new NotificationService(db,actor,access,atomic,clock);
        var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
        var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var lifecycle=new ShiftExpiryService(db,atomic,clock,supplies);
        var recover=typeof(StaffSupplyService).GetMethod("RecoverAcceptedConsumption",BindingFlags.NonPublic|BindingFlags.Instance)!;

        await transaction.CreateSavepointAsync("shortage",ct);
        // Make only the last item's actual batch unavailable; preceding items still pass preflight.
        var lastItem=balances[^1].ItemId;
        var batches=await db.StockBatches.Where(b=>b.BranchId==allocation.BranchId&&b.ItemId==lastItem&&b.Status=="READY").ToArrayAsync(ct);
        Assert.NotEmpty(batches);
        foreach(var batch in batches)batch.Status="EXPIRED";
        await db.SaveChangesAsync(ct);
        var ledgers=await db.StaffSupplyLedgers.CountAsync(ct);
        var reservations=await db.SupplyReservations.CountAsync(ct);
        var movements=await db.StockMovements.CountAsync(ct);
        await (Task)recover.Invoke(supplies,[allocation.BranchId,ct])!;
        Assert.Equal(ledgers,await db.StaffSupplyLedgers.CountAsync(ct));
        Assert.Equal(reservations,await db.SupplyReservations.CountAsync(ct));
        Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
        foreach(var b in await db.StaffSupplyBalances.AsNoTracking().Where(b=>b.AllocationId==allocationId).ToArrayAsync(ct))
            Assert.Equal(baseline[b.BalanceId],(b.Used,b.Reserved,b.Returned,b.InitialIssued,b.AdditionalIssued));
        Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.EntityKey==workId.ToString()&&a.Action=="ACCEPTED_SUPPLIES_SHORT",ct));
        await (Task)recover.Invoke(supplies,[allocation.BranchId,ct])!;
        Assert.Equal(1,await db.AuditLogs.CountAsync(a=>a.EntityKey==workId.ToString()&&a.Action=="ACCEPTED_SUPPLIES_SHORT",ct));
        clock.Now=new(2026,11,27,9,1,0,DateTimeKind.Utc);
        await lifecycle.CloseExpired(allocation.BranchId,ct);
        Assert.Equal("WORKING",day.Status);Assert.Equal("OPEN",allocation.Status);
        Assert.False(await db.StaffSupplyLedgers.AnyAsync(l=>l.WorkId==workId&&l.Kind=="USE",ct));
        await transaction.RollbackToSavepointAsync("shortage",ct);db.ChangeTracker.Clear();

        clock.Now=new(2026,11,27,8,5,0,DateTimeKind.Utc);
        await lifecycle.Synchronize(ct);
        var uses=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.WorkId==workId&&l.Kind=="USE").OrderBy(l=>l.LedgerId).ToArrayAsync(ct);
        Assert.NotEmpty(uses);
        var capacity=await (from w in db.WorkItems join r in db.Rooms on w.RoomId equals r.RoomId join t in db.RoomTypes on r.RoomTypeId equals t.RoomTypeId where w.WorkId==workId select t.Capacity).SingleAsync(ct);
        foreach(var b in balances){
            Assert.Equal(capacity*b.PerGuestQuantity,uses.Where(l=>l.BalanceId==b.BalanceId).Sum(l=>l.Quantity));
            Assert.Equal(baseline[b.BalanceId].Used+capacity*b.PerGuestQuantity,(await db.StaffSupplyBalances.AsNoTracking().SingleAsync(x=>x.BalanceId==b.BalanceId,ct)).Used);
        }
        Assert.All(uses,l=>Assert.Contains(l.BalanceId,baseline.Keys));
        Assert.Equal("WORKING",(await db.WorkdayAssignments.AsNoTracking().SingleAsync(d=>d.AssignmentId==day.AssignmentId,ct)).Status);
        await lifecycle.Synchronize(ct);
        Assert.Equal(uses.Select(l=>l.LedgerId),await db.StaffSupplyLedgers.Where(l=>l.WorkId==workId&&l.Kind=="USE").OrderBy(l=>l.LedgerId).Select(l=>l.LedgerId).ToArrayAsync(ct));
        clock.Now=new(2026,11,27,9,1,0,DateTimeKind.Utc);
        await lifecycle.Synchronize(ct);
        Assert.Equal("WORKING",(await db.WorkdayAssignments.AsNoTracking().SingleAsync(d=>d.AssignmentId==day.AssignmentId,ct)).Status);
        var endingDay=await db.WorkdayAssignments.SingleAsync(d=>d.AssignmentId==day.AssignmentId,ct);
        endingDay.Status="ENDED";endingDay.EndedAt=clock.Now;
        await db.SaveChangesAsync(ct);
        await lifecycle.CloseExpired(allocation.BranchId,ct);
        Assert.NotEqual("CLOSED",(await db.StaffSupplyAllocations.AsNoTracking().SingleAsync(a=>a.AllocationId==allocationId,ct)).Status);
        clock.Now=clock.Now.AddMinutes(5);
        await lifecycle.CloseExpired(allocation.BranchId,ct);
        Assert.Equal("CLOSED",(await db.StaffSupplyAllocations.AsNoTracking().SingleAsync(a=>a.AllocationId==allocationId,ct)).Status);
        foreach(var b in await db.StaffSupplyBalances.AsNoTracking().Where(b=>b.AllocationId==allocationId).ToArrayAsync(ct)){
            Assert.Equal(b.InitialIssued+b.AdditionalIssued-b.Used,b.Returned);Assert.Equal(0,b.Reserved);
            Assert.Equal(b.Returned,await db.StaffSupplyLedgers.Where(l=>l.BalanceId==b.BalanceId&&l.Kind=="RETURN").SumAsync(l=>l.Quantity,ct));
        }
        var documentCount=await db.SupplyStockDocuments.CountAsync(ct);var movementCount=await db.StockMovements.CountAsync(ct);var ledgerCount=await db.StaffSupplyLedgers.CountAsync(ct);
        await lifecycle.Synchronize(ct);
        Assert.Equal(documentCount,await db.SupplyStockDocuments.CountAsync(ct));Assert.Equal(movementCount,await db.StockMovements.CountAsync(ct));Assert.Equal(ledgerCount,await db.StaffSupplyLedgers.CountAsync(ct));
        var afterHistory=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.WorkId==historicalWorkId&&l.Kind=="USE").OrderBy(l=>l.LedgerId).ToArrayAsync(ct);
        Assert.Equal(history.Select(l=>(l.LedgerId,l.Quantity,l.BalanceId,l.ReservationId)),afterHistory.Select(l=>(l.LedgerId,l.Quantity,l.BalanceId,l.ReservationId)));
        await transaction.RollbackAsync(ct);db.ChangeTracker.Clear();
        Assert.False(await db.StaffSupplyLedgers.AnyAsync(l=>l.WorkId==workId&&l.Kind=="USE",ct));
        Assert.Equal("WORKING",(await db.WorkdayAssignments.AsNoTracking().SingleAsync(d=>d.AssignmentId==day.AssignmentId,ct)).Status);
    }
}
