using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
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
public sealed class HistoricalRoomAssessmentDatabaseTests
{
    private sealed class FixtureClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026,11,27,8,25,0,TimeSpan.Zero);
    }

    [TaskSupplyDatabaseFact]
    public async Task HistoricalConsumptionWithoutAuditUsesOriginalNormAfterLiveConfigurationChanges()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_TASK_SUPPLY_TEST")!;
        Assert.Equal("HotelOpsCompactWorkdayTestNormSql20261127",new SqlConnectionStringBuilder(connection).InitialCatalog);
        var options=new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options;
        await using var db=new HotelOpsCompactDbContext(options);
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct=timeout.Token;
        var originalUses=await db.StaffSupplyLedgers.CountAsync(ct);
        await using(var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct)){
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.TaskSupplyTestFixture',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);
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
                SecurityStamp=user.SecurityStamp, CreatedAt=now, ExpiresAt=now.AddHours(1) };
            db.UserSessions.Add(session);

            await db.SaveChangesAsync(ct);
            var actor = new Actor(new HttpContextAccessor { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.UserId.ToString()),
                    new Claim("session_id",session.SessionId.ToString())],"isolated-fixture")) }});
            var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);
            var notifications=new NotificationService(db,actor,access,atomic,clock);
            var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
            var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
            await (Task)typeof(StaffSupplyService).GetMethod("Lock",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[db,ct])!;
            var movements=await db.StockMovements.CountAsync(ct);

            var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AssignmentId==day.AssignmentId,ct);

            var balances=await db.StaffSupplyBalances.Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct);
            Assert.NotEmpty(balances);

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
                AssignedBy=user.UserId,AssignmentStatus="ACTIVE",AssignedAt=now,AcceptedAt=now});await db.SaveChangesAsync(ct);
            Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
            work.Status="IN_PROGRESS";work.ActualStartAt=now;
            db.WorkEvents.Add(new WorkEvent{BranchId=work.BranchId,WorkId=work.WorkId,EventType="START",ActorUserId=user.UserId,
                OperationKey=Guid.NewGuid(),FromStatus="ASSIGNED",ToStatus="IN_PROGRESS",OccurredAt=now,SnapshotJson="{\"materialConsumptionMode\":\"FULL_NORM_AT_COMPLETION\"}"});
            await db.SaveChangesAsync(ct);
            var balance=balances.First(b=>b.PerGuestQuantity>0);
            var originalNorm=capacity*balance.PerGuestQuantity;
            allocation.Status="CLOSED";allocation.ClosedAt=now;
            var roomType=await db.RoomTypes.SingleAsync(t=>t.RoomTypeId==room.RoomTypeId,ct);
            roomType.Capacity=capacity+3;roomType.IsActive=false;
            var configuration=await db.SupplyConfigurations.SingleAsync(c=>c.ConfigurationId==balance.ConfigurationId,ct);
            configuration.IsCurrent=false;
            var profile=await db.ItemLifecycleProfiles.SingleAsync(p=>p.ItemId==balance.ItemId&&p.BranchId==work.BranchId,ct);
            profile.Classification="REUSABLE";
            await db.SaveChangesAsync(ct);
            await transaction.CreateSavepointAsync("history",ct);
            foreach(var quantity in new[]{1m,0m}){
                var reservation=new SupplyReservation{WorkId=work.WorkId,RoomId=room.RoomId,BalanceId=balance.BalanceId,
                    Capacity=capacity,PerGuestQuantity=balance.PerGuestQuantity,Quantity=quantity,
                    UsableQuantity=originalNorm-quantity,AssessedAt=now,AssessedBy=user.UserId,
                    Status="CONSUMED",CreatedAt=now,ResolvedAt=now};
                db.SupplyReservations.Add(reservation);await db.SaveChangesAsync(ct);
                if(quantity>0){
                    db.StaffSupplyLedgers.Add(new StaffSupplyLedger{BalanceId=balance.BalanceId,WorkId=work.WorkId,
                        ReservationId=reservation.ReservationId,Kind="USE",Quantity=quantity,OperationKey=Guid.NewGuid(),
                        ActorUserId=user.UserId,OccurredAt=now});
                    await db.SaveChangesAsync(ct);
                }
                var useCount=await db.StaffSupplyLedgers.CountAsync(ct);
                var result=await supplies.RoomAssessment(work.WorkId,ct);
                Assert.True(result.Confirmed);Assert.True(result.ReadOnly);Assert.True(result.Historical);
                Assert.Equal(allocation.AllocationId,result.AllocationId);
                var line=Assert.Single(result.Lines);
                Assert.Equal(originalNorm,line.StandardQuantity);Assert.Equal(quantity,line.RequiredQuantity);
                Assert.Equal(0,line.ShortageQuantity);
                Assert.Equal(useCount,await db.StaffSupplyLedgers.CountAsync(ct));
                Assert.False(await db.AuditLogs.AnyAsync(a=>a.EntityType=="WorkItem"&&a.EntityKey==work.WorkId.ToString(),ct));
                Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
                await transaction.RollbackToSavepointAsync("history",ct);db.ChangeTracker.Clear();
                await transaction.CreateSavepointAsync("history",ct);
            }
            // A consumed reservation without its positive USE cannot become a fresh live demand.
            db.SupplyReservations.Add(new SupplyReservation{WorkId=work.WorkId,RoomId=room.RoomId,BalanceId=balance.BalanceId,
                Capacity=capacity,PerGuestQuantity=balance.PerGuestQuantity,Quantity=1,
                UsableQuantity=originalNorm-1,AssessedAt=now,AssessedBy=user.UserId,Status="CONSUMED",CreatedAt=now,ResolvedAt=now});
            await db.SaveChangesAsync(ct);
            var invalid=await Assert.ThrowsAsync<ApiException>(()=>supplies.RoomAssessment(work.WorkId,ct));
            Assert.Equal("MATERIAL_CONSUMPTION_INVALID",invalid.Code);
            await transaction.RollbackToSavepointAsync("history",ct);db.ChangeTracker.Clear();
            await transaction.CreateSavepointAsync("history",ct);
            var otherRoom=await db.Rooms.FirstAsync(r=>r.RoomId!=room.RoomId&&r.BranchId==work.BranchId,ct);
            db.SupplyReservations.Add(new SupplyReservation{WorkId=work.WorkId,RoomId=room.RoomId,BalanceId=balance.BalanceId,
                Capacity=capacity,PerGuestQuantity=balance.PerGuestQuantity,Quantity=0,
                UsableQuantity=originalNorm,AssessedAt=now,AssessedBy=user.UserId,Status="CONSUMED",CreatedAt=now,ResolvedAt=now});
            await db.SaveChangesAsync(ct);
            // SQL rejects inserting a mismatched room; exercise the service guard against a pending work change.
            var pendingWork=await db.WorkItems.SingleAsync(w=>w.WorkId==work.WorkId,ct);
            pendingWork.RoomId=otherRoom.RoomId;
            var wrongRoom=await Assert.ThrowsAsync<ApiException>(()=>supplies.RoomAssessment(work.WorkId,ct));
            Assert.Equal("SUPPLY_BRANCH_MISMATCH",wrongRoom.Code);
            pendingWork.RoomId=room.RoomId;
            pendingWork.BranchId=await db.Branches.Where(b=>b.BranchId!=work.BranchId&&b.IsActive).Select(b=>b.BranchId).FirstAsync(ct);
            // Invoke only the historical helper so authorization remains independently tested by Access tests.
            var wrongBranch=await Assert.ThrowsAsync<ApiException>(async()=>await (Task)typeof(StaffSupplyService)
                .GetMethod("HistoricalAssessment",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(supplies,[pendingWork,ct])!);
            Assert.Equal("SUPPLY_BRANCH_MISMATCH",wrongBranch.Code);
            await transaction.RollbackAsync(ct);
        }
        await using var check=new HotelOpsCompactDbContext(options);
        Assert.Equal(originalUses,await check.StaffSupplyLedgers.CountAsync(ct));
    }
}
