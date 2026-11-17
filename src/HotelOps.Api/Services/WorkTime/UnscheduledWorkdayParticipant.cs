using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed partial class SqlWorkTimeTransactionParticipant
{
    // Caller authorizes ATTENDANCE_RECORD and owns the same Serializable transaction and session gate.
    public Task<Workday> StartUnscheduledWorkdayAsync(long actor, long branch, long? department,
        string reason, CancellationToken ct) => RunAsync(async () =>
    {
        RequireLocks(actor);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 || reason.Contains('\0'))
            throw Failure("TIME_REASON_REQUIRED");
        if (!await WorkdayMembershipStore.InstalledAsync(db, ct)) throw Failure("TIME_SOURCE_UNRESOLVED");
        var now = await NowAsync(ct);
        await EligibleAsync(actor, branch, now, ct);
        if (await Working(actor).AnyAsync(ct)) throw Failure("TIME_ALREADY_WORKING");
        if (await Open(actor).AnyAsync(ct)) throw Failure("TIME_STATE_CONFLICT");
        var memberships = await WorkdayMembershipStore.ReadAsync(db, actor, branch, now, ct);
        var selected = WorkdayMembershipStore.Select(memberships, department);
        now = await NowAsync(ct);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")));
        var departmentIds = memberships.Select(d => d.DepartmentId).ToArray();
        // Use the same eligible, unclaimed population offered by OwnShifts; a client
        // cannot turn an available scheduled assignment into an unscheduled workday.
        if (await ScheduledWorkdayClaims.Unclaimed(db.ShiftAssignments.AsNoTracking(), db.Workdays.AsNoTracking())
            .AnyAsync(a => a.UserId == actor && a.AssignmentStatus == "ASSIGNED" && a.AssignedAt <= now
                && a.Shift.BranchId == branch && a.Shift.Status == "PLANNED" && a.Shift.ShiftDate == today
                && a.Shift.ScheduledEndAt > a.Shift.ScheduledStartAt && departmentIds.Contains(a.Shift.DepartmentId)
                && a.Shift.ShiftTemplate.BranchId == branch && a.Shift.ShiftTemplate.IsActive
                && a.Shift.Department.BranchId == branch && a.Shift.Department.IsActive, ct))
            throw Failure("TIME_SCHEDULED_ASSIGNMENT_AVAILABLE");
        var day = new Workday
        {
            UserId = actor, BranchId = branch, DepartmentId = selected,
            WorkDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))),
            ShiftAssignmentId = null, IsUnscheduled = true, UnscheduledReason = reason.Trim(),
            Status = "WORKING", StartedAt = now
        };
        db.Workdays.Add(day);
        await db.SaveChangesAsync(ct);
        Audit(branch, actor, "workdays", day.WorkdayId, "CREATE", null,
            new { day.UserId, day.DepartmentId, day.Status, day.IsUnscheduled, day.UnscheduledReason, day.StartedAt }, now);
        await db.SaveChangesAsync(ct);
        // Durable intent even when no Head currently qualifies. Delivery uses a separate transaction;
        // a failed delivery can never undo this workday or fabricate a recipient.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.unscheduled_workday_notifications(workday_id,status,created_at)
            VALUES({day.WorkdayId},'PENDING',{now})
            """, ct);
        return day;
    });
}
