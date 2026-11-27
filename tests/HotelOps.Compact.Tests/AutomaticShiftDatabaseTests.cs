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

public sealed class AutomaticShiftDatabaseFactAttribute : FactAttribute
{
    public AutomaticShiftDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOTELOPS_AUTO_SHIFT_TEST")))
            Skip = "Requires a backed-up, isolated HotelOpsCompactWorkdayTestAuto clone.";
    }
}

[Collection("Note2511 SQL")]
public sealed class AutomaticShiftDatabaseTests
{
    private sealed class ShiftClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    [AutomaticShiftDatabaseFact]
    public async Task InitialKitShortageIssuesNothingAndKeepsSnapshotForRefill()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_AUTO_SHIFT_TEST")!;
        Assert.Matches("^HotelOpsCompactWorkdayTest(?:(?:Auto|Norm)[0-9]+|NormSql20261127)$",new SqlConnectionStringBuilder(connection).InitialCatalog);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));var ct=timeout.Token;
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.TaskSupplyTestFixture',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture workday lock unavailable',1;",ct);
        var clock=new ShiftClock(new DateTime(2026,11,28,9,1,0,DateTimeKind.Utc));
        var old=await (from a in db.StaffSupplyAllocations join d in db.WorkdayAssignments on a.AssignmentId equals d.AssignmentId
            where a.Status!="CLOSED"&&d.Status=="ENDED" orderby a.AllocationId select a).FirstAsync(ct);
        var priorDays=await db.WorkdayAssignments.Where(d=>d.UserId==old.UserId&&d.Status=="WORKING").ToArrayAsync(ct);
        foreach(var prior in priorDays){prior.Status="ENDED";prior.EndedAt=clock.Now;}
        await db.SaveChangesAsync(ct);
        var shift=await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
            where s.BranchId==old.BranchId&&s.IsActive&&r.Code=="HK_STAFF"&&s.StartMinute==960&&s.EndMinute==1440 select s).SingleAsync(ct);
        var day=new WorkdayAssignment{ShiftId=shift.ShiftId,UserId=old.UserId,WorkDate=new DateOnly(2026,11,28),CreatedAt=clock.Now,CreatedBy=old.UserId};
        db.WorkdayAssignments.Add(day);await db.SaveChangesAsync(ct);
        // Existing real configuration only; no manufactured stock or custody.
        var configuration=await db.SupplyConfigurations.Where(c=>c.BranchId==old.BranchId&&c.IsCurrent&&c.IsActive&&c.SupplyGroup=="A"
            &&db.ItemLifecycleProfiles.Any(p=>p.BranchId==c.BranchId&&p.ItemId==c.ItemId&&p.Classification=="CONSUMABLE")).FirstAsync(ct);
        configuration.IsCurrent=false;
        await db.SaveChangesAsync(ct);
        db.SupplyConfigurations.Add(new SupplyConfiguration{BranchId=configuration.BranchId,ItemId=configuration.ItemId,
            AssetId=configuration.AssetId,SupplyGroup=configuration.SupplyGroup,WarehouseId=configuration.WarehouseId,
            UnitId=configuration.UnitId,InitialQuantity=99999999,MinimumStock=configuration.MinimumStock,
            PerGuestQuantity=configuration.PerGuestQuantity,IsActive=true,IsCurrent=true,Revision=configuration.Revision+1,
            UpdatedBy=configuration.UpdatedBy,UpdatedAt=clock.Now});
        await db.SaveChangesAsync(ct);
        var actor=new Actor(new HttpContextAccessor());var atomic=new Atomic(db);var access=new Access(db,actor,clock);var audit=new Audit(db,actor,clock);
        var notifications=new NotificationService(db,actor,access,atomic,clock);
        var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
        var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var lifecycle=new ShiftExpiryService(db,atomic,clock,supplies);
        await lifecycle.CloseExpired(null,ct);
        // Prior ended custody must settle before a new kit can be allocated.
        Assert.NotEqual("CLOSED",old.Status);
        clock.Now=clock.Now.AddMinutes(5);
        await lifecycle.CloseExpired(null,ct);
        var movements=await db.StockMovements.CountAsync(ct);
        await lifecycle.Synchronize(ct);
        var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AssignmentId==day.AssignmentId,ct);
        Assert.Equal("WORKING",day.Status);Assert.Equal("SHORT",allocation.Status);
        Assert.Contains("UC-153",allocation.ShortageReason);
        Assert.False(await db.SupplyStockDocuments.AnyAsync(d=>d.AllocationId==allocation.AllocationId,ct));
        Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
        Assert.All(await db.StaffSupplyBalances.Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct),b=>Assert.Equal(0,b.InitialIssued));
        Assert.True(await db.Notifications.AnyAsync(n=>n.RecipientUserId==old.UserId&&n.NotificationType=="SUPPLY_INITIAL_SHORT",ct));
        var notificationCount=await db.Notifications.CountAsync(ct);
        await lifecycle.Synchronize(ct);
        Assert.Equal(notificationCount,await db.Notifications.CountAsync(ct));
        Assert.Equal(1,await db.StaffSupplyAllocations.CountAsync(a=>a.AssignmentId==day.AssignmentId,ct));
        await transaction.RollbackAsync(ct);
    }

    [AutomaticShiftDatabaseFact]
    public async Task StaleInitialKitUnitDoesNotAbortShiftSynchronizationOrIssuePartialStock()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_AUTO_SHIFT_TEST")!;
        Assert.Matches("^HotelOpsCompactWorkdayTest(?:(?:Auto|Norm)[0-9]+|NormSql20261127)$",new SqlConnectionStringBuilder(connection).InitialCatalog);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(120));var ct=timeout.Token;
        await using var db=new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.TaskSupplyTestFixture',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture workday lock unavailable',1;",ct);
        var clock=new ShiftClock(new DateTime(2026,11,28,9,1,0,DateTimeKind.Utc));
        var old=await (from a in db.StaffSupplyAllocations join d in db.WorkdayAssignments on a.AssignmentId equals d.AssignmentId
            where a.Status!="CLOSED"&&d.Status=="ENDED" orderby a.AllocationId select a).FirstAsync(ct);
        var priorDays=await db.WorkdayAssignments.Where(d=>d.UserId==old.UserId&&d.Status=="WORKING").ToArrayAsync(ct);
        foreach(var prior in priorDays){prior.Status="ENDED";prior.EndedAt=clock.Now;}
        await db.SaveChangesAsync(ct);
        var shift=await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
            where s.BranchId==old.BranchId&&s.IsActive&&r.Code=="HK_STAFF"&&s.StartMinute==960&&s.EndMinute==1440 select s).SingleAsync(ct);
        var day=new WorkdayAssignment{ShiftId=shift.ShiftId,UserId=old.UserId,WorkDate=new DateOnly(2026,11,28),CreatedAt=clock.Now,CreatedBy=old.UserId};
        db.WorkdayAssignments.Add(day);await db.SaveChangesAsync(ct);
        // Existing real configuration only; no manufactured stock or custody.
        var configuration=await db.SupplyConfigurations.Where(c=>c.BranchId==old.BranchId&&c.IsCurrent&&c.IsActive&&c.SupplyGroup=="A"
            &&db.ItemLifecycleProfiles.Any(p=>p.BranchId==c.BranchId&&p.ItemId==c.ItemId&&p.Classification=="CONSUMABLE")).FirstAsync(ct);
        var staleUnit=await db.ReferenceValues.Where(r=>r.Kind=="UOM"&&r.IsActive&&r.ReferenceId!=configuration.UnitId)
            .Select(r=>r.ReferenceId).FirstAsync(ct);
        var available=(Task<decimal>)typeof(StaffSupplyService).Assembly.GetType("HotelOps.Api.Compact.Services.StockBatchAccounting")!
            .GetMethod("Available",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null,[db,configuration.WarehouseId!.Value,configuration.ItemId!.Value,clock.Now,ct])!;
        Assert.True(await available>=configuration.InitialQuantity);
        configuration.IsCurrent=false;
        await db.SaveChangesAsync(ct);
        db.SupplyConfigurations.Add(new SupplyConfiguration{BranchId=configuration.BranchId,ItemId=configuration.ItemId,
            AssetId=configuration.AssetId,SupplyGroup=configuration.SupplyGroup,WarehouseId=configuration.WarehouseId,
            UnitId=staleUnit,InitialQuantity=configuration.InitialQuantity,MinimumStock=configuration.MinimumStock,
            PerGuestQuantity=configuration.PerGuestQuantity,IsActive=true,IsCurrent=true,Revision=configuration.Revision+1,
            UpdatedBy=configuration.UpdatedBy,UpdatedAt=clock.Now});
        await db.SaveChangesAsync(ct);
        var actor=new Actor(new HttpContextAccessor());var atomic=new Atomic(db);var access=new Access(db,actor,clock);var audit=new Audit(db,actor,clock);
        var notifications=new NotificationService(db,actor,access,atomic,clock);
        var documents=new DocumentService(db,access,actor,atomic,audit,clock,notifications);
        var supplies=new StaffSupplyService(db,access,actor,atomic,audit,clock,documents);
        var lifecycle=new ShiftExpiryService(db,atomic,clock,supplies);
        await lifecycle.CloseExpired(null,ct);
        // Prior ended custody must settle before a new kit can be allocated.
        Assert.NotEqual("CLOSED",old.Status);
        clock.Now=clock.Now.AddMinutes(5);
        await lifecycle.CloseExpired(null,ct);
        var movements=await db.StockMovements.CountAsync(ct);
        await lifecycle.Synchronize(ct);
        var allocation=await db.StaffSupplyAllocations.SingleAsync(a=>a.AssignmentId==day.AssignmentId,ct);
        Assert.Equal("WORKING",day.Status);Assert.Equal("SHORT",allocation.Status);
        Assert.Contains("UC-153",allocation.ShortageReason);
        Assert.Contains("đơn vị",allocation.ShortageReason);
        Assert.True(await ShiftCommandAccess.CanOperate(db,day.UserId,old.BranchId,clock.Now,ct));
        Assert.False(await db.StaffSupplyBalances.AnyAsync(b=>b.AllocationId==allocation.AllocationId,ct));
        Assert.False(await db.StaffSupplyLedgers.AnyAsync(l=>db.StaffSupplyBalances.Any(b=>b.BalanceId==l.BalanceId&&b.AllocationId==allocation.AllocationId),ct));
        Assert.False(await db.SupplyStockDocuments.AnyAsync(d=>d.AllocationId==allocation.AllocationId,ct));
        Assert.Equal(movements,await db.StockMovements.CountAsync(ct));
        Assert.All(await db.StaffSupplyBalances.Where(b=>b.AllocationId==allocation.AllocationId).ToArrayAsync(ct),b=>Assert.Equal(0,b.InitialIssued));
        Assert.True(await db.Notifications.AnyAsync(n=>n.RecipientUserId==old.UserId&&n.NotificationType=="SUPPLY_INITIAL_SHORT",ct));
        var notificationCount=await db.Notifications.CountAsync(ct);
        await lifecycle.Synchronize(ct);
        Assert.Equal(notificationCount,await db.Notifications.CountAsync(ct));
        Assert.Equal(1,await db.StaffSupplyAllocations.CountAsync(a=>a.AssignmentId==day.AssignmentId,ct));
        await transaction.RollbackAsync(ct);
    }

    [AutomaticShiftDatabaseFact]
    public async Task ScheduledStaffWaitForOldCustodyBeforeNewShiftAndOperateWithoutCheckIn()
    {
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_AUTO_SHIFT_TEST")!;
        Assert.Matches("^HotelOpsCompactWorkdayTest(?:(?:Auto|Norm)[0-9]+|NormSql20261127)$", new SqlConnectionStringBuilder(connection).InitialCatalog);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var ct = timeout.Token;
        await using var db = new HotelOpsCompactDbContext(new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection).Options);
        // All fixture changes, including system audits, are rolled back in the isolated clone.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.TaskSupplyTestFixture',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture lock unavailable',1;",ct);
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=60000; IF @r<0 THROW 51999,'Fixture workday lock unavailable',1;",ct);
        var clock = new ShiftClock(new DateTime(2026, 11, 28, 8, 59, 0, DateTimeKind.Utc));
        var old = await (from allocation in db.StaffSupplyAllocations
            join day in db.WorkdayAssignments on allocation.AssignmentId equals day.AssignmentId
            where allocation.Status != "CLOSED" && day.Status == "ENDED"
            orderby allocation.AllocationId select allocation).FirstAsync(ct);
        var oldVersion = old.RowVersion.ToArray();
        var oldStatus = old.Status;
        var date = new DateOnly(2026, 11, 28);
        Assert.False(await db.WorkdayAssignments.AnyAsync(a => a.WorkDate == date, ct));
        string[] codes = ["HK_STAFF", "HOUSEKEEPING_SUPERVISOR", "RECEPTION", "STOREKEEPER", "MAINTENANCE_STAFF", "MAINTENANCE_SUPERVISOR"];
        var scheduled = new Dictionary<string, WorkdayAssignment>();
        foreach (var code in codes)
        {
            var shift = await (from s in db.WorkdayShifts join r in db.Roles on s.RoleId equals r.RoleId
                where s.BranchId == old.BranchId && s.IsActive && r.Code == code && s.StartMinute == 960 && s.EndMinute == 1440
                select s).SingleAsync(ct);
            var user = code == "HK_STAFF" ? old.UserId : await (from ur in db.UserRoles join u in db.Users on ur.UserId equals u.UserId
                where ur.RoleId == shift.RoleId && ur.BranchId == shift.BranchId && ur.RevokedAt == null
                    && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
                orderby u.UserId select u.UserId).FirstAsync(ct);
            var row = new WorkdayAssignment { ShiftId = shift.ShiftId, UserId = user, WorkDate = date, CreatedAt = clock.Now, CreatedBy = user };
            db.WorkdayAssignments.Add(row);
            scheduled.Add(code, row);
        }
        await db.SaveChangesAsync(ct);
        var atomic = new Atomic(db);
        // Background execution has no HTTP principal; authorization comes from the persisted assignment.
        var systemActor = new Actor(new HttpContextAccessor());
        var systemAccess = new Access(db, systemActor, clock);
        var systemAudit = new Audit(db, systemActor, clock);
        var systemNotifications = new NotificationService(db, systemActor, systemAccess, atomic, clock);
        var systemDocuments = new DocumentService(db, systemAccess, systemActor, atomic, systemAudit, clock, systemNotifications);
        var systemSupplies = new StaffSupplyService(db, systemAccess, systemActor, atomic, systemAudit, clock, systemDocuments);
        var lifecycle = new ShiftExpiryService(db, atomic, clock, systemSupplies);
        await (Task)typeof(StaffSupplyService).GetMethod("RecoverAcceptedConsumption",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(systemSupplies,[old.BranchId,ct])!;
        var priorDays=await db.WorkdayAssignments.Where(d=>d.UserId==old.UserId&&d.Status=="WORKING").ToArrayAsync(ct);
        foreach(var prior in priorDays){prior.Status="ENDED";prior.EndedAt=clock.Now;}
        await db.SaveChangesAsync(ct);
        await lifecycle.Synchronize(ct);
        foreach (var day in scheduled.Values)
        {
            Assert.Equal("PLANNED", day.Status);
            Assert.False(await ShiftCommandAccess.CanOperate(db, day.UserId, old.BranchId, clock.Now, ct));
        }

        var staff = scheduled["HK_STAFF"];
        var employee = await db.Users.SingleAsync(u => u.UserId == staff.UserId, ct);
        employee.AccountStatus = "INACTIVE";
        await db.SaveChangesAsync(ct);
        clock.Now = clock.Now.AddMinutes(1); // Exactly the scheduled start.
        await lifecycle.Synchronize(ct);
        Assert.Equal("PLANNED", staff.Status);
        foreach (var day in scheduled.Where(x => x.Key != "HK_STAFF").Select(x => x.Value))
        {
            Assert.Equal("WORKING", day.Status);
            Assert.True(await ShiftCommandAccess.CanOperate(db, day.UserId, old.BranchId, clock.Now, ct));
        }
        employee.AccountStatus = "ACTIVE";
        await db.SaveChangesAsync(ct);
        await lifecycle.Synchronize(ct);
        Assert.Equal("PLANNED",staff.Status);
        Assert.False(await db.StaffSupplyAllocations.AnyAsync(a=>a.AssignmentId==staff.AssignmentId,ct));
        clock.Now = clock.Now.AddMinutes(5);
        await lifecycle.Synchronize(ct);
        Assert.Equal("WORKING", staff.Status);
        Assert.Equal(clock.Now, staff.StartedAt); // No backdating through the inactive period.
        Assert.Equal("CLOSED", old.Status);
        Assert.NotEqual(oldVersion, old.RowVersion);
        Assert.True(await db.StaffSupplyAllocations.AnyAsync(a => a.AssignmentId == staff.AssignmentId, ct));
        Assert.True(await ShiftCommandAccess.CanOperate(db, staff.UserId, old.BranchId, clock.Now, ct, "CLEANING_EXECUTE", true));
        var otherBranch = await db.Branches.Where(b => b.BranchId != old.BranchId && b.IsActive).Select(b => b.BranchId).FirstAsync(ct);
        Assert.False(await ShiftCommandAccess.CanOperate(db, staff.UserId, otherBranch, clock.Now, ct, "CLEANING_EXECUTE", true));

        var supervisor = scheduled["HOUSEKEEPING_SUPERVISOR"].UserId;
        var supervisorUser = await db.Users.SingleAsync(u => u.UserId == supervisor, ct);
        var session = new UserSession { UserId = supervisor, SecurityStamp = supervisorUser.SecurityStamp,
            TokenHash = RandomNumberGenerator.GetBytes(32), CreatedAt = clock.Now, ExpiresAt = clock.Now.AddHours(12) };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(ct);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, supervisor.ToString()), new Claim("session_id", session.SessionId.ToString())
            ], "isolated-sql-test")) } };
        var actor = new Actor(accessor);
        var access = new Access(db, actor, clock);
        var audit = new Audit(db, actor, clock);
        var notifications = new NotificationService(db, actor, access, atomic, clock);
        var documents = new DocumentService(db, access, actor, atomic, audit, clock, notifications);
        var supplies = new StaffSupplyService(db, access, actor, atomic, audit, clock, documents);
        var requests = new RequestWorkflowService(db, access, actor, atomic, audit, clock, supplies, documents, notifications);
        var work = new WorkService(db, access, actor, atomic, audit, clock, notifications, documents,
            new RoomReadinessService(db, actor, clock), supplies, requests);
        var options = JsonSerializer.SerializeToElement(await work.CleaningCreationOptions(old.BranchId, ct));
        Assert.True(options.GetProperty("canCreate").GetBoolean());
        Assert.Contains(options.GetProperty("candidates").EnumerateArray(), candidate => candidate.GetProperty("UserId").GetInt64() == staff.UserId);

        var template = await db.Rooms.AsNoTracking().Where(r => r.BranchId == old.BranchId && r.IsActive).FirstAsync(ct);
        var room = new Room { BranchId = template.BranchId, LocationId = template.LocationId, RoomTypeId = template.RoomTypeId,
            DepartmentId = template.DepartmentId, Code = "AUTO-SHIFT-TEST", OperationalStatus = "DIRTY", IsActive = true,
            CreatedAt = clock.Now, TurnoverNo = 1 };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(ct);
        var created = (ResourceDto)await work.Create(new CreateWorkRequest(old.BranchId, "CLEANING", "Automatic shift SQL verification",
            null, null, room.RoomId, null, null, null, null, null, null, null, null,
            RoomRowVersion: Convert.ToBase64String(room.RowVersion), AssigneeUserId: staff.UserId, DurationMinutes: 30), ct);
        var workId = (long)created.Fields["workId"]!;
        Assert.True(await db.StaffSupplyAllocations.AnyAsync(a => a.AssignmentId == staff.AssignmentId, ct));
        var staffSession = new UserSession { UserId = employee.UserId, SecurityStamp = employee.SecurityStamp,
            TokenHash = RandomNumberGenerator.GetBytes(32), CreatedAt = clock.Now, ExpiresAt = clock.Now.AddHours(12) };
        db.UserSessions.Add(staffSession);
        await db.SaveChangesAsync(ct);
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, staff.UserId.ToString()), new Claim("session_id", staffSession.SessionId.ToString())
        ], "isolated-sql-test"));
        var oldTimer = await db.WorkIntervals.SingleAsync(t => t.UserId == staff.UserId && t.ClosedAt == null, ct);
        var allocationsBeforeStart = await db.StaffSupplyAllocations.CountAsync(ct);
        var blocked = await Assert.ThrowsAsync<ApiException>(() => work.Act(workId, "start",
            new ActionRequest(Convert.ToBase64String((byte[])created.Fields["rowVersion"]!), Guid.NewGuid()), ct));
        Assert.Equal("ACTIVE_WORK_IN_PROGRESS", blocked.Code);
        Assert.Null(oldTimer.ClosedAt);
        Assert.Null((await db.WorkAssignments.SingleAsync(a => a.WorkId == workId && a.AssignmentStatus == "ACTIVE", ct)).AcceptedAt);
        Assert.Equal(allocationsBeforeStart, await db.StaffSupplyAllocations.CountAsync(ct));
        // Resolve the previous timer through the real work transition, only in this rolled-back fixture.
        var previousWork = await db.WorkItems.SingleAsync(w => w.WorkId == oldTimer.WorkId, ct);
        await work.Act(previousWork.WorkId, "pause", new ActionRequest(Convert.ToBase64String(previousWork.RowVersion),
            Guid.NewGuid(), "Pause previous work for isolated SQL regression test"), ct);
        Assert.NotNull(oldTimer.ClosedAt);
        var accepted = (ResourceDto)await work.Act(workId, "start",
            new ActionRequest(Convert.ToBase64String((byte[])created.Fields["rowVersion"]!), Guid.NewGuid()), ct);
        Assert.Equal("IN_PROGRESS", accepted.Fields["status"]);
        Assert.Equal(workId, (await db.WorkIntervals.SingleAsync(t => t.UserId == staff.UserId && t.ClosedAt == null, ct)).WorkId);
        var newAllocation = await db.StaffSupplyAllocations.SingleAsync(a => a.AssignmentId == staff.AssignmentId, ct);
        Assert.NotEqual(old.AllocationId, newAllocation.AllocationId);
        Assert.True(await db.SupplyReservations.AnyAsync(r => r.WorkId == workId && r.Status == "CONSUMED", ct));
        Assert.False(await db.SupplyStockDocuments.AnyAsync(d=>d.AllocationId==newAllocation.AllocationId&&d.Purpose=="ADDITIONAL",ct));
        var issued=await db.StaffSupplyBalances.Where(b=>b.AllocationId==newAllocation.AllocationId).ToArrayAsync(ct);
        Assert.All(issued,b=>Assert.Equal(b.InitialTarget,b.InitialIssued));
        var capacity=await db.RoomTypes.Where(t=>t.RoomTypeId==room.RoomTypeId).Select(t=>t.Capacity).SingleAsync(ct);
        Assert.All(issued,b=>{Assert.Equal(capacity*b.PerGuestQuantity,b.Used);Assert.Equal(0,b.Reserved);});
        Assert.Equal(issued.Count(b=>b.Used>0),await db.StaffSupplyLedgers.CountAsync(l=>l.WorkId==workId&&l.Kind=="USE",ct));
        var warehouseMovements=await db.StockMovements.CountAsync(ct);
        var usedBefore=issued.Select(b=>b.Used).ToArray();
        var form=await supplies.RoomAssessment(workId,ct);
        Assert.True(form.Confirmed);
        Assert.True(form.ReadOnly);
        // Merely viewing the norm never consumes supplies.
        Assert.Equal(usedBefore,issued.Select(b=>b.Used));
        Assert.Equal(warehouseMovements,await db.StockMovements.CountAsync(ct));
        Assert.Equal("CLOSED",old.Status);
        var week = new WorkdayService(db, access, actor, atomic, audit, clock, lifecycle, supplies);
        var schedule = JsonSerializer.SerializeToElement(await week.Week(old.BranchId, date, ct));
        Assert.Equal(staff.AssignmentId, schedule.GetProperty("active").GetProperty("AssignmentId").GetInt64());
        Assert.DoesNotContain(schedule.GetProperty("pendingReconciliations").EnumerateArray(),
            pending => pending.GetProperty("AssignmentId").GetInt64() == old.AssignmentId);

        var count = await db.AuditLogs.CountAsync(a => a.Action == ShiftExpiryService.AutomaticStartAction, ct);
        await lifecycle.Synchronize(ct);
        Assert.Equal(count, await db.AuditLogs.CountAsync(a => a.Action == ShiftExpiryService.AutomaticStartAction, ct));
        var grant = await db.UserRoles.SingleAsync(r => r.UserId == staff.UserId && r.BranchId == old.BranchId
            && r.RoleId == db.WorkdayShifts.Where(s => s.ShiftId == staff.ShiftId).Select(s => s.RoleId).Single() && r.RevokedAt == null, ct);
        grant.RevokedAt = clock.Now;
        await db.SaveChangesAsync(ct);
        Assert.False(await ShiftCommandAccess.CanOperate(db, staff.UserId, old.BranchId, clock.Now, ct));

        clock.Now = new DateTime(2026, 11, 28, 17, 0, 0, DateTimeKind.Utc);
        await lifecycle.Synchronize(ct);
        foreach (var day in scheduled.Values)
        {
            if(day==staff){Assert.Equal("WORKING",day.Status);Assert.Null(day.EndedAt);}
            else {
                Assert.Equal("ENDED", day.Status);
                var shift=await db.WorkdayShifts.SingleAsync(s=>s.ShiftId==day.ShiftId,ct);
                var zone=await db.Branches.Where(b=>b.BranchId==old.BranchId).Select(b=>b.Timezone).SingleAsync(ct);
                Assert.Equal(ShiftExpiryService.Boundary(day.WorkDate,shift.EndMinute,zone),day.EndedAt);
            }
            Assert.False(await ShiftCommandAccess.CanOperate(db, day.UserId, old.BranchId, clock.Now, ct));
        }
        Assert.Equal("CLOSED", old.Status);
        Assert.NotEqual(oldVersion, old.RowVersion);
        await transaction.RollbackAsync(ct);
    }
}
