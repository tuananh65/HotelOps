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

public sealed class TaskSupplyDatabaseTests
{
    private sealed class FixtureClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 25, 16, 25, 0, TimeSpan.Zero);
    }

    [TaskSupplyDatabaseFact]
    public async Task RealSqlStartDoesNotIssueAndAcceptanceConsumesOnlyActualPlacement()
    {
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        Assert.Matches("^HotelOpsCompactWorkdayTestTaskSupplies[0-9]+$", new SqlConnectionStringBuilder(connection).InitialCatalog);
        var options = new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options;
        await using var db = new HotelOpsCompactDbContext(options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = timeout.Token;
        var originalDocuments = await db.InventoryDocuments.CountAsync(ct);
        await using (var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct))
        {
            // Serialise fixture setup across independently launched test runners.
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.TaskSupplyTestFixture',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);
            var day = await (from a in db.WorkdayAssignments join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
                             join r in db.Roles on s.RoleId equals r.RoleId
                             where a.Status == "PLANNED" && a.WorkDate == new DateOnly(2026,11,25)
                                && r.Code == "HK_STAFF" && s.EndMinute == 1440
                             select a).FirstAsync(ct);
            var shift = await db.WorkdayShifts.SingleAsync(s => s.ShiftId == day.ShiftId, ct);
            var clock = new FixtureClock();
            var now = clock.GetUtcNow().UtcDateTime;
            var user = await db.Users.SingleAsync(u => u.UserId == day.UserId, ct);
            var session = new UserSession { UserId=user.UserId, TokenHash=RandomNumberGenerator.GetBytes(32),
                SecurityStamp=user.SecurityStamp, CreatedAt=now, ExpiresAt=now.AddHours(1) };
            db.UserSessions.Add(session);
            day.Status="WORKING";day.StartedAt=now;
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
            var movements=await db.StockMovements.CountAsync(ct);
            var alerts=await db.Notifications.CountAsync(n=>n.NotificationType=="SUPPLY_INITIAL_SHORT",ct);
            await Hook("Start",day.AssignmentId,ct);
            await Hook("Start",day.AssignmentId,ct);
            Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
            Assert.Equal(alerts,await db.Notifications.CountAsync(n=>n.NotificationType=="SUPPLY_INITIAL_SHORT",ct));
            var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AssignmentId==day.AssignmentId,ct);
            var balances=await db.StaffSupplyBalances.Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct);
            Assert.NotEmpty(balances);Assert.All(balances,b=>Assert.Equal(0,b.InitialIssued+b.AdditionalIssued));
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
            var readyBatches=await db.StockBatches.Where(b=>b.BranchId==shift.BranchId&&b.Status=="READY").ToArrayAsync(ct);
            Assert.NotEmpty(readyBatches);
            foreach(var batch in readyBatches)batch.Status="EXPIRED";
            await db.SaveChangesAsync(ct);
            var shortage=await Assert.ThrowsAsync<ApiException>(()=>Hook("Reserve",work.WorkId,user.UserId,Guid.NewGuid(),ct,false));
            Assert.Equal("STOCK_INSUFFICIENT",shortage.Code);
            Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
            Assert.False(await db.SupplyReservations.AnyAsync(r=>r.WorkId==work.WorkId,ct));
            foreach(var batch in readyBatches)batch.Status="READY";
            await db.SaveChangesAsync(ct);
            await Hook("Reserve",work.WorkId,user.UserId,Guid.NewGuid(),ct,false);
            Assert.All(balances,b=>{Assert.Equal(capacity*b.PerGuestQuantity,b.AdditionalIssued);Assert.Equal(b.AdditionalIssued,b.Reserved);});
            var afterIssue=await db.StockMovements.CountAsync(ct);
            await Hook("Reserve",work.WorkId,user.UserId,Guid.NewGuid(),ct,false);
            Assert.Equal(afterIssue,await db.StockMovements.CountAsync(ct));
            work.Status="IN_PROGRESS";work.ActualStartAt=now;await db.SaveChangesAsync(ct);
            var assessment=await supplies.RoomAssessment(work.WorkId,ct);
            var lines=assessment.Select(r=>new AssessRoomSupplyLine(r.ReservationId,r.RequiredQuantity-1,r.RowVersion,
                r.RequiredQuantity-1,0,0,1)).ToArray();
            await supplies.AssessRoom(work.WorkId,new(Guid.NewGuid(),lines),ct);
            await Hook("Consume",work.WorkId,Guid.NewGuid(),ct);
            Assert.All(balances,b=>{Assert.Equal(1,b.Used);Assert.Equal(0,b.Reserved);});
            Assert.Equal(afterIssue,await db.StockMovements.CountAsync(ct));
            await transaction.RollbackAsync(ct);
        }
        await using var check=new HotelOpsCompactDbContext(options);
        Assert.Equal(originalDocuments,await check.InventoryDocuments.CountAsync(ct));
    }
}
