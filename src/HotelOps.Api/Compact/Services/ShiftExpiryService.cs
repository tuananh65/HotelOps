using System.Text.Json;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed class ShiftExpiryService(HotelOpsCompactDbContext db, Atomic atomic, TimeProvider clock, StaffSupplyService? supplies=null)
{
    public const string ExpiredWithoutStartAction = "AUTO_CANCEL_EXPIRED";
    public const string AutomaticStartAction = "AUTO_START";
    public const string UnfinishedAtExpiryAction = "SHIFT_EXPIRED_UNFINISHED";

    // Run before request transactions: never acquire the workday lock while holding
    // the supply lock. Expire all branches first because a user has one active shift.
    public Task<int> Synchronize(CancellationToken ct) => atomic.Run(async () =>
    {
        var count = await CloseExpired(null, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var earliest = DateOnly.FromDateTime(now.AddDays(-1));
        var latest = DateOnly.FromDateTime(now.AddDays(1));
        var candidates = await (from a in db.WorkdayAssignments
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            join b in db.Branches on s.BranchId equals b.BranchId
            where a.Status == "PLANNED" && a.WorkDate >= earliest && a.WorkDate <= latest
                && s.IsActive && b.IsActive
                && db.Users.Any(u => u.UserId == a.UserId && u.AccountStatus == "ACTIVE"
                    && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null)
                && db.Memberships.Any(m => m.UserId == a.UserId && m.BranchId == s.BranchId
                    && m.Kind == "BRANCH_ACCESS" && m.ValidFrom <= now && (m.ValidTo == null || m.ValidTo > now))
                && db.Memberships.Any(m => m.UserId == a.UserId && m.BranchId == s.BranchId
                    && m.DepartmentId == s.DepartmentId && m.Kind == "EMPLOYMENT"
                    && m.ValidFrom <= now && (m.ValidTo == null || m.ValidTo > now))
                && db.UserRoles.Any(r => r.UserId == a.UserId && r.RoleId == s.RoleId && r.BranchId == s.BranchId
                    && r.AssignedAt <= now && r.RevokedAt == null && (r.ExpiresAt == null || r.ExpiresAt > now))
                && db.Roles.Any(r => r.RoleId == s.RoleId && r.IsActive)
                && db.Departments.Any(d => d.DepartmentId == s.DepartmentId && d.BranchId == s.BranchId && d.IsActive)
                && db.RolePermissions.Any(g => g.RoleId == s.RoleId
                    && (g.DataScope == "OWN" || g.DataScope == "BRANCH" || g.DataScope == "ALL")
                    && db.Permissions.Any(p => p.PermissionId == g.PermissionId && p.IsActive && p.Code == "ATTENDANCE_RECORD"))
            orderby a.WorkDate, s.StartMinute, a.AssignmentId
            select new { Assignment = a, s.BranchId, s.StartMinute, s.EndMinute, b.Timezone }).ToArrayAsync(ct);
        var working = (await db.WorkdayAssignments.Where(a => a.Status == "WORKING")
            .Select(a => a.UserId).ToArrayAsync(ct)).ToHashSet();
        foreach (var item in candidates)
        {
            var row = item.Assignment;
            if (working.Contains(row.UserId) || await db.StaffSupplyAllocations.AnyAsync(a => a.UserId == row.UserId && a.Status != "CLOSED", ct)) continue;
            if (supplies != null && await supplies.HasPendingMissingCustody(row.UserId, ct)) continue;
            var before = JsonSerializer.Serialize(Output.Record(row));
            if (!TryStart(row, item.StartMinute, item.EndMinute, item.Timezone, now)) continue;
            working.Add(row.UserId);
            // Prior custody must be settled before activating another shift or issuing its kit.
            db.AuditLogs.Add(new AuditLog
            {
                BranchId = item.BranchId, ActorUserId = null, EntityType = "Workday",
                EntityKey = row.AssignmentId.ToString(), Action = AutomaticStartAction,
                OperationKey = Guid.NewGuid(), BeforeJson = before,
                AfterJson = JsonSerializer.Serialize(Output.Record(row)), OccurredAt = now
            });
            await db.SaveChangesAsync(ct);
            Output.Require(supplies!=null,"STAFF_SUPPLY_SERVICE_REQUIRED");
            await supplies!.AllocateAutomatic(row,ct);
            count++;
        }
        if(supplies!=null){await supplies.RecoverAcceptedConsumption(null,ct);await supplies.RoutePendingRefillsToStore(ct);}
        await db.SaveChangesAsync(ct);
        return count;
    }, ct);

    public static bool TryStart(WorkdayAssignment row, int startMinute, int endMinute, string timezone, DateTime now)
    {
        if (row.Status != "PLANNED" || row.StartedAt != null || row.EndedAt != null || row.CreatedAt > now
            || now < Boundary(row.WorkDate, startMinute, timezone)
            || now >= Boundary(row.WorkDate, endMinute, timezone)) return false;
        row.Status = "WORKING";
        // Do not backdate attendance across downtime or a mid-shift eligibility change.
        row.StartedAt = now;
        return true;
    }

    public static DateTime Boundary(DateOnly day, int minute, string timezone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue).AddMinutes(minute),
            TimeZoneInfo.FindSystemTimeZoneById(timezone));

    public Task<int> CloseExpired(long? branchId, CancellationToken ct) => atomic.Run(async () =>
    {
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51410,'Workday lock unavailable.',1;", ct);
        // Serialize against refill before reading/updating attendance rows: refill owns
        // StaffSupplies while validating those same rows, so acquiring it later can deadlock.
        await StaffSupplyService.Lock(db,ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if(supplies!=null)await supplies.RecoverAcceptedConsumption(branchId,ct);
        var active = await (from a in db.WorkdayAssignments
            join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            join b in db.Branches on s.BranchId equals b.BranchId
            // Housekeeping attendance remains open until explicit manual closure.
            where (a.Status == "PLANNED" || (a.Status == "WORKING" && a.StartedAt <= now))
                && (branchId == null || s.BranchId == branchId)
            select new { Assignment = a, s.BranchId, s.EndMinute, b.Timezone, RoleCode = db.Roles.Where(r => r.RoleId == s.RoleId).Select(r => r.Code).First() }).ToArrayAsync(ct);
        var count = 0;
        foreach (var item in active)
        {
            var row = item.Assignment;
            var before = JsonSerializer.Serialize(Output.Record(row));
            if (item.RoleCode == "HK_STAFF" && row.Status == "WORKING" && row.StartedAt != null
                && Boundary(row.WorkDate, item.EndMinute, item.Timezone) <= now)
                await NotifyUnfinished(row, item.BranchId, now, ct);
            if (!TryExpire(row, item.EndMinute, item.Timezone, now, item.RoleCode)) continue;
            db.AuditLogs.Add(new AuditLog
            {
                BranchId = item.BranchId, ActorUserId = null, EntityType = "Workday",
                EntityKey = row.AssignmentId.ToString(),
                Action = row.Status == "CANCELLED" ? ExpiredWithoutStartAction : "AUTO_END", OperationKey = Guid.NewGuid(),
                BeforeJson = before, AfterJson = JsonSerializer.Serialize(Output.Record(row)), OccurredAt = now
            });
            count++;
        }
        await db.SaveChangesAsync(ct);
        if(supplies!=null)await supplies.CloseEndedAllocations(branchId,ct);
        else Output.Require(!await db.StaffSupplyAllocations.AnyAsync(a=>a.Status!="CLOSED"&&(branchId==null||a.BranchId==branchId)
            &&db.WorkdayAssignments.Any(d=>d.AssignmentId==a.AssignmentId&&d.Status=="ENDED"),ct),"STAFF_SUPPLY_SERVICE_REQUIRED");
        return count;
    }, ct);

    private async Task NotifyUnfinished(WorkdayAssignment day, long branch, DateTime now, CancellationToken ct)
    {
        var key = day.AssignmentId.ToString();
        if (await db.AuditLogs.AnyAsync(a => a.BranchId == branch && a.EntityType == "Workday"
            && a.EntityKey == key && a.Action == UnfinishedAtExpiryAction, ct)) return;
        var workIds = await (from a in db.WorkAssignments.AsNoTracking()
            join w in db.WorkItems on a.WorkId equals w.WorkId
            where a.AssigneeUserId == day.UserId && a.BranchId == branch && a.AssignmentStatus == "ACTIVE"
                && (w.Kind == "CLEANING" || w.Kind == "REWORK")
                && (w.Status == "ASSIGNED" || w.Status == "PENDING_ACCEPTANCE" || w.Status == "ACCEPTED" || w.Status == "IN_PROGRESS" || w.Status == "ON_HOLD")
            orderby w.WorkId select w.WorkId).ToArrayAsync(ct);
        if (workIds.Length == 0) return;
        var recipients = await (from ur in db.UserRoles.AsNoTracking()
            join r in db.Roles on ur.RoleId equals r.RoleId
            join u in db.Users on ur.UserId equals u.UserId
            where ur.BranchId == branch && (r.Code == "HOUSEKEEPING_SUPERVISOR" || r.Code == "OPERATION_MANAGER") && r.IsActive
                && ur.AssignedAt <= now && ur.RevokedAt == null && (ur.ExpiresAt == null || ur.ExpiresAt > now)
                && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null
                && db.Memberships.Any(m => m.UserId == u.UserId && m.BranchId == branch && m.Kind == "BRANCH_ACCESS"
                    && m.ValidFrom <= now && (m.ValidTo == null || m.ValidTo > now))
            select u.UserId).Distinct().ToArrayAsync(ct);
        if (recipients.Length == 0) return;
        var name = await db.Users.Where(u => u.UserId == day.UserId).Select(u => u.FullName).SingleAsync(ct);
        var eventKey = Guid.NewGuid();
        var message = $"{name} đã hết giờ ca nhưng còn {workIds.Length} công việc vệ sinh chưa hoàn tất. Chỉ tiếp tục việc đã bắt đầu; không cần duyệt quá giờ. OM theo dõi báo cáo, giám sát hỗ trợ bàn giao khi cần.";
        foreach (var recipient in recipients)
            db.Notifications.Add(new Notification { BranchId = branch, RecipientUserId = recipient,
                WorkId = workIds[0], EventKey = eventKey, NotificationType = UnfinishedAtExpiryAction,
                Title = "Báo cáo công việc còn mở khi hết giờ ca", Message = message, AvailableAt = now, CreatedAt = now });
        // Protected by the workday transaction lock, persisted with the notifications.
        db.AuditLogs.Add(new AuditLog { BranchId = branch, ActorUserId = null, EntityType = "Workday",
            EntityKey = key, Action = UnfinishedAtExpiryAction, OperationKey = eventKey,
            AfterJson = JsonSerializer.Serialize(new { day.AssignmentId, workIds, recipients }), OccurredAt = now });
    }

    public static bool TryExpire(WorkdayAssignment row, int endMinute, string timezone, DateTime now, string? roleCode = null)
    {
        if (row.Status == "WORKING" && roleCode == "HK_STAFF") return false;
        if (row.Status == "PLANNED")
        {
            if (row.StartedAt != null || row.EndedAt != null
                || Boundary(row.WorkDate, endMinute, timezone).AddHours(1) > now) return false;
            // Never fabricate attendance: ENDED requires a real start in the database.
            // The system audit distinguishes this expiry from a manual cancellation.
            row.Status = "CANCELLED";
            return true;
        }
        if (row.Status != "WORKING" || row.StartedAt == null || row.StartedAt > now) return false;
        var end = Boundary(row.WorkDate, endMinute, timezone);
        if (end > now) return false;
        row.Status = "ENDED";
        // Historical late starts must still satisfy ended_at >= started_at.
        row.EndedAt = row.StartedAt > end ? row.StartedAt : end;
        return true;
    }
}

public sealed class ShiftExpiryWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<ShiftExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Compact:AutoEndShiftsEnabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ShiftExpiryService>().Synchronize(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Automatic shift synchronization failed; transaction rolled back."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
