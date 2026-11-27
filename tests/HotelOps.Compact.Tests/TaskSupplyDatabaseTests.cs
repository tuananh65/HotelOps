using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class TaskSupplyDatabaseFactAttribute : FactAttribute
{
    public TaskSupplyDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")))
            Skip = "Requires an isolated TaskSupplies clone with the owner-approved receipt fixture.";
    }
}

[Collection("Note2511 SQL")]
public sealed class TaskSupplyDatabaseTests
{
    private sealed class AcceptanceFailure : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,
            InterceptionResult<DbDataReader> result,CancellationToken ct=default)
        {
            if(Armed&&command.CommandText.Contains("INSERT INTO [audit_logs]")
                &&command.Parameters.Cast<DbParameter>().Any(p=>Equals(p.Value,"SUPPLIES_CONSUMED_AT_ACCEPTANCE"))){
                Armed=false;
                throw new InvalidOperationException("Injected acceptance audit failure after consumption writes");
            }
            return base.ReaderExecutingAsync(command,eventData,result,ct);
        }
    }

    private sealed class FixtureClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 11, 27, 8, 25, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [TaskSupplyDatabaseFact]
    public async Task AcceptanceConsumesFullNormOnceAndCompletionAfterShiftDoesNotChargeAgain()
    {
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127", new SqlConnectionStringBuilder(connection).InitialCatalog);
        var failure=new AcceptanceFailure();
        var options = new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).AddInterceptors(failure).Options;
        await using var db = new HotelOpsCompactDbContext(options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = timeout.Token;
        var originalDocuments = await db.InventoryDocuments.CountAsync(ct);
        await using (var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            // Serialise fixture setup across independently launched test runners.
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.TaskSupplyTestFixture',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture workday lock unavailable',1;",ct);
            var shift = await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
                where s.IsActive && r.Code == "HK_STAFF" && s.StartMinute == 480 && s.EndMinute == 960
                orderby s.ShiftId select s).FirstAsync(ct);
            var staffId = await (from ur in db.UserRoles join u in db.Users on ur.UserId equals u.UserId
                where ur.RoleId == shift.RoleId && ur.BranchId == shift.BranchId && ur.RevokedAt == null
                    && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
                orderby u.UserId select u.UserId).FirstAsync(ct);
            var day = await db.WorkdayAssignments.SingleAsync(d => d.ShiftId==shift.ShiftId && d.UserId==staffId
                && d.WorkDate==new DateOnly(2026,11,27) && d.Status=="WORKING",ct);
            var clock = new FixtureClock();
            var now = clock.GetUtcNow().UtcDateTime;
            var user = await db.Users.SingleAsync(u => u.UserId == day.UserId, ct);
            var session = new UserSession { UserId=user.UserId, TokenHash=RandomNumberGenerator.GetBytes(32),
                SecurityStamp=user.SecurityStamp, CreatedAt=now, ExpiresAt=now.AddHours(12) };
            db.UserSessions.Add(session);

            await db.SaveChangesAsync(ct);
            var actor = new Actor(new HttpContextAccessor { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.UserId.ToString()),
                    new Claim("session_id",session.SessionId.ToString())],"isolated-fixture")) }});
            var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
            var notifications=new NotificationService(db,actor,access,atomic,clock);
            var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
            var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
            async Task Hook(string name,params object[] args) =>
                await (Task)typeof(StaffSupplyService).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(supplies,args)!;
            await (Task)typeof(StaffSupplyService).GetMethod("Lock",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[db,ct])!;
            var movements=await db.StockMovements.CountAsync(ct);

            var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AssignmentId==day.AssignmentId,ct);
            Assert.Equal("OPEN",allocation.Status);
            var balances=await db.StaffSupplyBalances.Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct);
            Assert.NotEmpty(balances);
            Assert.All(balances,b=>{Assert.Equal(b.InitialTarget,b.InitialIssued);Assert.Equal(0,b.AdditionalIssued);});
            Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
            movements=await db.StockMovements.CountAsync(ct);
            var room=await db.Rooms.FirstAsync(r=>r.BranchId==shift.BranchId&&r.IsActive&&r.OperationalStatus!="OCCUPIED"
                && !db.WorkItems.Any(w=>w.RoomId==r.RoomId&&(w.Kind=="CLEANING"||w.Kind=="REWORK")
                    &&w.Status!="COMPLETED"&&w.Status!="CANCELLED"&&w.Status!="CLOSED"),ct);
            var capacity=await db.RoomTypes.Where(t=>t.RoomTypeId==room.RoomTypeId).Select(t=>t.Capacity).SingleAsync(ct);
            Assert.True(capacity>1);
            var work=new WorkItem{BranchId=shift.BranchId,WorkNo=Guid.NewGuid().ToString("N"),Kind="CLEANING",Status="ASSIGNED",
                Title="Isolated per-task supply verification",RoomId=room.RoomId,TurnoverNo=room.TurnoverNo,
                CreatedBy=user.UserId,CreatedAt=now,DueAt=now.AddMinutes(30)};
            db.WorkItems.Add(work);await db.SaveChangesAsync(ct);
            db.WorkAssignments.Add(new WorkAssignment{BranchId=shift.BranchId,WorkId=work.WorkId,AssigneeUserId=user.UserId,
                AssignedBy=user.UserId,AssignmentStatus="ACTIVE",AssignedAt=now});await db.SaveChangesAsync(ct);
            Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
            var before=await supplies.RoomAssessment(work.WorkId,ct);
            Assert.False(before.Confirmed);
            Assert.True(before.ReadOnly);
            Assert.NotEmpty(before.Lines);
            Assert.All(before.Lines,l=>Assert.Equal(capacity*balances.Single(b=>b.ItemId==l.ItemId).PerGuestQuantity,l.StandardQuantity));
            Assert.False(await db.SupplyReservations.AnyAsync(r=>r.WorkId==work.WorkId,ct));
            Assert.All(balances,b=>{Assert.Equal(0,b.Used);Assert.Equal(0,b.Reserved);});
            var requests=new RequestWorkflowService(db,access,actor,atomic,audit,clock,supplies,documents,notifications);
            var workService=new WorkService(db,access,actor,atomic,audit,clock,notifications,documents,new RoomReadinessService(db,actor,clock),supplies,requests);
            async Task Accept() {
                var current=await db.WorkItems.AsNoTracking().SingleAsync(w=>w.WorkId==work.WorkId,ct);
                await workService.Act(work.WorkId,"accept",new ActionRequest(Convert.ToBase64String(current.RowVersion),Guid.NewGuid()),ct);
            }
            await transaction.CreateSavepointAsync("completion",ct);
            await Accept();
            var expected=before.Lines.ToDictionary(l=>l.ItemId,l=>l.StandardQuantity);
            var actual=await db.StaffSupplyBalances.AsNoTracking().Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct);
            Assert.All(actual,b=>{Assert.Equal(expected[b.ItemId],b.Used);Assert.Equal(0,b.Reserved);});
            var uses=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.WorkId==work.WorkId&&l.Kind=="USE").ToArrayAsync(ct);
            Assert.Equal(expected.Count(x=>x.Value>0),uses.Length);
            Assert.Equal(expected.Values.Sum(),uses.Sum(l=>l.Quantity));
            await Hook("ConsumeForAcceptance",work.WorkId,Guid.NewGuid(),ct);
            Assert.Equal(uses.Select(l=>l.LedgerId).Order(),(await db.StaffSupplyLedgers.Where(l=>l.WorkId==work.WorkId&&l.Kind=="USE").Select(l=>l.LedgerId).ToArrayAsync(ct)).Order());
            Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
            await transaction.RollbackToSavepointAsync("completion",ct);db.ChangeTracker.Clear();
            Assert.False(await db.StaffSupplyLedgers.AnyAsync(l=>l.WorkId==work.WorkId&&l.Kind=="USE",ct));
            // A failure after custody writes must be undone by the enclosing acceptance transaction.
            await transaction.CreateSavepointAsync("failedCompletion",ct);
            failure.Armed=true;
            var injected=await Assert.ThrowsAsync<DbUpdateException>(()=>Accept());
            Assert.Contains("Injected acceptance audit failure",injected.InnerException!.Message);
            Assert.False(failure.Armed);
            await transaction.RollbackToSavepointAsync("failedCompletion",ct);db.ChangeTracker.Clear();
            Assert.False(await db.StaffSupplyLedgers.AnyAsync(l=>l.WorkId==work.WorkId&&l.Kind=="USE",ct));
            Assert.False(await db.AuditLogs.AnyAsync(a=>a.EntityType=="WorkItem"&&a.EntityKey==work.WorkId.ToString()&&a.Action=="SUPPLIES_CONSUMED_AT_ACCEPTANCE",ct));
            Assert.All(await db.StaffSupplyBalances.AsNoTracking().Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct),b=>{Assert.Equal(0,b.Used);Assert.Equal(0,b.Reserved);});
            await transaction.CreateSavepointAsync("expiredCustody",ct);
            var batches=await db.StockBatches.Where(b=>b.BranchId==shift.BranchId&&b.Status=="READY").ToArrayAsync(ct);
            foreach(var batch in batches)batch.Status="EXPIRED";
            await db.SaveChangesAsync(ct);
            var shortage=await Assert.ThrowsAsync<ApiException>(()=>Accept());
            Assert.Equal("STAFF_SUPPLIES_INSUFFICIENT",shortage.Code);
            Assert.Null((await db.WorkAssignments.AsNoTracking().SingleAsync(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE",ct)).AcceptedAt);
            Assert.Equal("ASSIGNED",(await db.WorkItems.AsNoTracking().SingleAsync(w=>w.WorkId==work.WorkId,ct)).Status);
            Assert.False(await db.SupplyReservations.AnyAsync(r=>r.WorkId==work.WorkId,ct));
            Assert.Contains("UC-153",shortage.Message);
            Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
            Assert.False(await db.StaffSupplyLedgers.AnyAsync(l=>l.WorkId==work.WorkId&&l.Kind=="USE",ct));
            await transaction.RollbackToSavepointAsync("expiredCustody",ct);db.ChangeTracker.Clear();
            await Accept();
            var started=await db.WorkItems.SingleAsync(w=>w.WorkId==work.WorkId,ct);
            started.Status="IN_PROGRESS";started.ActualStartAt=now;
            await db.SaveChangesAsync(ct);
            var acceptedUses=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.WorkId==work.WorkId&&l.Kind=="USE").OrderBy(l=>l.LedgerId).ToArrayAsync(ct);
            Assert.NotEmpty(acceptedUses);
            var refill=await requests.CreateRefill(new(allocation.AllocationId,[new(balances[0].ItemId,1)],
                "Additional supplies requested before end of shift",Guid.NewGuid()),ct);
            var refillEvents=await db.RequestWorkflowEvents.CountAsync(e=>e.WorkId==refill.Request.WorkId,ct);
            // An ended allocation schedules its return; custody remains unchanged during the delay.
            var currentDay=await db.WorkdayAssignments.SingleAsync(d=>d.AssignmentId==day.AssignmentId,ct);
            currentDay.Status="ENDED";currentDay.EndedAt=now;
            await db.SaveChangesAsync(ct);
            await Hook("CloseEndedAllocations",(long?)shift.BranchId,ct);
            Assert.NotEqual("CLOSED",allocation.Status);
            Assert.False(await db.SupplyStockDocuments.AnyAsync(d=>d.AllocationId==allocation.AllocationId&&d.Purpose=="RETURN",ct));
            clock.Now=clock.Now.AddMinutes(5);
            await Hook("CloseEndedAllocations",(long?)shift.BranchId,ct);
            var returned=await db.StaffSupplyBalances.AsNoTracking().Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct);
            Assert.All(returned,b=>{Assert.True(b.Returned>0);Assert.Equal(b.InitialIssued-b.Used,b.Returned);Assert.Equal(expected[b.ItemId],b.Used);});
            Assert.True(await db.SupplyStockDocuments.AnyAsync(d=>d.AllocationId==allocation.AllocationId&&d.Purpose=="RETURN",ct));
            Assert.True(await db.StockMovements.CountAsync(ct)>movements);
            var cancelled=await db.RequestWorkflows.AsNoTracking().SingleAsync(f=>f.WorkId==refill.Request.WorkId,ct);
            Assert.Equal("CANCELLED",cancelled.Status);Assert.Null(cancelled.QueueRoleId);Assert.Null(cancelled.HandlerUserId);
            Assert.Equal(refillEvents,await db.RequestWorkflowEvents.CountAsync(e=>e.WorkId==refill.Request.WorkId,ct));
            Assert.True(await db.RequestWorkflowLines.AnyAsync(l=>l.WorkId==refill.Request.WorkId&&l.RequestedQuantity==1,ct));
            Assert.True(await db.AuditLogs.AnyAsync(a=>a.EntityKey==refill.Request.WorkId.ToString()&&a.Action=="AUTO_CANCEL_SHIFT_ENDED"&&a.ActorUserId==null,ct));
            Assert.False(await db.SupplyStockDocuments.AnyAsync(d=>d.RequestWorkId==refill.Request.WorkId&&d.Purpose=="ADDITIONAL",ct));
            var returnCount=await db.StaffSupplyLedgers.CountAsync(l=>l.Kind=="RETURN",ct);
            await Hook("CloseEndedAllocations",(long?)shift.BranchId,ct);
            Assert.Equal(returnCount,await db.StaffSupplyLedgers.CountAsync(l=>l.Kind=="RETURN",ct));
            Assert.Equal("IN_PROGRESS",(await db.WorkItems.AsNoTracking().SingleAsync(w=>w.WorkId==work.WorkId,ct)).Status);
            Assert.Equal("CLOSED",(await db.StaffSupplyAllocations.AsNoTracking().SingleAsync(a=>a.AllocationId==allocation.AllocationId,ct)).Status);
            clock.Now=clock.Now.AddHours(9);
            await Hook("ValidateForCompletion",work.WorkId,Guid.NewGuid(),ct);
            var completedUses=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.WorkId==work.WorkId&&l.Kind=="USE").OrderBy(l=>l.LedgerId).ToArrayAsync(ct);
            Assert.Equal(acceptedUses.Select(l=>(l.LedgerId,l.Quantity,l.BalanceId,l.ReservationId)),completedUses.Select(l=>(l.LedgerId,l.Quantity,l.BalanceId,l.ReservationId)));
            await transaction.RollbackAsync(ct);
        }
        await using var check=new HotelOpsCompactDbContext(options);
        Assert.Equal(originalDocuments,await check.InventoryDocuments.CountAsync(ct));
    }
}
