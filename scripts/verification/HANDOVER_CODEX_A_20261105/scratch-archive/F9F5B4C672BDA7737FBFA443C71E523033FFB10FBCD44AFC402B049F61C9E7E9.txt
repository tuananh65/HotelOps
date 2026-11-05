using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal sealed record Dev1ResponsibilityReview(IReadOnlyList<string> Blockers, IReadOnlyList<string> Unresolved)
{
    // Unsupported means no authoritative model for the required responsibility, rather
    // than an empty SQL result. Keep distinct from known blockers and ambiguous mappings.
    public IReadOnlyList<string> Unsupported { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Task47 technical approval: explicit designated-user responsibilities only.
/// Uses caller context/transaction; not a complete eligibility decision or a locking protocol.
/// Unresolved modules/data must prevent any future Deactivate mutation.
/// </summary>
internal sealed class ApprovedDev1ResponsibilityQueries(HotelOpsDbContext db)
{
    internal async Task<Dev1ResponsibilityReview> ReadAsync(long targetId, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        ct.ThrowIfCancellationRequested();
        var blockers = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new HashSet<string>(StringComparer.Ordinal)
        {
            "OPERATIONAL_TASK_CONTRACT", "INTERNAL_REQUEST_CONTRACT", "SHIFT_CONTRACT", "WORKDAY_CONTRACT",
            "LINE_MANAGER_CONTRACT"
        };
        // Read all designated references, including history and contradictory legacy rows.
        // Materialize only lifecycle scalars; classify exact stored vocabulary ordinally.
        var assignments = await (from assignment in db.CleaningAssignments.AsNoTracking()
            where assignment.AssigneeUserId == targetId
            join parent in db.CleaningTasks.AsNoTracking() on assignment.CleaningTaskId equals parent.CleaningTaskId into parents
            from parent in parents.DefaultIfEmpty()
            select new { assignment.AssignmentStatus, assignment.AssignedAt, assignment.EndedAt,
                Parent = parent == null ? null : parent.Status }).ToArrayAsync(ct);
        foreach (var row in assignments)
        {
            if (row.EndedAt < row.AssignedAt || row.AssignmentStatus == "ACTIVE" && row.EndedAt != null
                || row.AssignmentStatus != "ACTIVE" && row.EndedAt == null)
            { unresolved.Add("CLEANING_DATA_INCONSISTENCY"); continue; }
            if (row.AssignmentStatus is not ("ACTIVE" or "COMPLETED" or "REASSIGNED" or "CANCELLED")
                || row.Parent is not ("PENDING" or "IN_PROGRESS" or "COMPLETED" or "CANCELLED"))
            { unresolved.Add("CLEANING_UNRESOLVED"); continue; }
            if (row.AssignmentStatus != "ACTIVE") continue;
            if (row.Parent is "PENDING" or "IN_PROGRESS") blockers.Add("CLEANING_ASSIGNMENT");
            else if (row.Parent == "CANCELLED") unresolved.Add("CLEANING_DATA_INCONSISTENCY");
            // SQL COMPLETED cannot prove SRS final completion vs Pending Inspection.
            else unresolved.Add("CLEANING_TERMINAL_MAPPING");
        }
        var reworks = await db.CleaningReworks.AsNoTracking().Where(x => x.AssigneeUserId == targetId)
            .Select(x => new { x.Status, x.CompletedAt, x.RequestedAt }).ToArrayAsync(ct);
        foreach (var row in reworks)
        {
            if (row.Status is "PENDING" or "IN_PROGRESS")
            {
                if (row.CompletedAt != null) unresolved.Add("REWORK_DATA_INCONSISTENCY");
                else blockers.Add("CLEANING_REWORK");
            }
            else if (row.Status == "COMPLETED")
            {
                if (row.CompletedAt == null || row.CompletedAt < row.RequestedAt) unresolved.Add("REWORK_DATA_INCONSISTENCY");
            }
            else if (row.Status != "CANCELLED") unresolved.Add("REWORK_UNRESOLVED");
        }
        var inspections = await db.RoomInspections.AsNoTracking().Where(x => x.InspectorUserId == targetId)
            .Select(x => new { x.Status, x.Result }).ToArrayAsync(ct);
        foreach (var row in inspections)
        {
            if (row.Status is "PENDING" or "IN_PROGRESS")
            {
                if (row.Result != null) unresolved.Add("INSPECTION_DATA_INCONSISTENCY");
                else blockers.Add("INSPECTION_ASSIGNEE");
            }
            else if (row.Status == "COMPLETED")
            {
                if (row.Result is not ("PASS" or "FAIL" or "CONDITIONAL")) unresolved.Add("INSPECTION_DATA_INCONSISTENCY");
            }
            else if (row.Status != "CANCELLED") unresolved.Add("INSPECTION_UNRESOLVED");
        }
        // Only assigned_to designates defect responsibility; never infer it from inspector,
        // checklist author, parent creator, or other historical FK attribution.
        var defects = await db.InspectionDefects.AsNoTracking().Where(x => x.AssignedTo == targetId)
            .Select(x => new { x.Status, x.ResolvedAt }).ToArrayAsync(ct);
        foreach (var row in defects)
        {
            if (row.Status is "OPEN" or "IN_PROGRESS")
            {
                if (row.ResolvedAt != null) unresolved.Add("DEFECT_DATA_INCONSISTENCY");
                else blockers.Add("INSPECTION_DEFECT_ASSIGNEE");
            }
            else if (row.Status == "RESOLVED")
            {
                if (row.ResolvedAt == null) unresolved.Add("DEFECT_DATA_INCONSISTENCY");
            }
            else if (row.Status != "CANCELLED") unresolved.Add("DEFECT_UNRESOLVED");
        }
        if (await db.Departments.AsNoTracking().AnyAsync(x => x.ManagerUserId == targetId && x.IsActive, ct))
            blockers.Add("DEPARTMENT_MANAGER");
        // V1 positive evidence only: a nondeleted active employee explicitly still
        // designates this manager. Account INACTIVE alone does not end employment.
        // Release/transfer rules for other references remain LINE_MANAGER_CONTRACT.
        var subordinates = await db.Users.AsNoTracking().Where(x => x.ManagerUserId == targetId)
            .Select(x => new { x.DeletedAt, x.EmploymentStatus }).ToArrayAsync(ct);
        foreach (var row in subordinates)
        {
            if (row.DeletedAt != null) continue;
            // Classify ordinally: CI SQL matching must not certify unknown casing.
            if (row.EmploymentStatus == "ACTIVE") blockers.Add("LINE_MANAGER_DESIGNATION");
            else unresolved.Add("LINE_MANAGER_EMPLOYMENT_MAPPING");
        }
        // Only actual open attendance is positive activity evidence. A scheduled
        // end (even in the past) is not a completed duty or an acknowledgment.
        // No new attendance/assignment status vocabulary is invented here.
        var shifts = await (from assignment in db.ShiftAssignments.AsNoTracking()
            where assignment.UserId == targetId
            join shift in db.WorkShifts.AsNoTracking() on assignment.ShiftId equals shift.ShiftId into parents
            from shift in parents.DefaultIfEmpty()
            join attendance in db.ShiftAttendances.AsNoTracking() on assignment.ShiftAssignmentId equals attendance.ShiftAssignmentId into attendances
            from attendance in attendances.DefaultIfEmpty()
            select new { assignment.AssignmentStatus, assignment.AssignedAt,
                ParentExists = shift != null,
                Start = shift == null ? (DateTime?)null : shift.ScheduledStartAt,
                End = shift == null ? (DateTime?)null : shift.ScheduledEndAt,
                CheckIn = attendance == null ? (DateTime?)null : attendance.ActualCheckInAt,
                CheckOut = attendance == null ? (DateTime?)null : attendance.ActualCheckOutAt }).ToArrayAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var row in shifts)
        {
            if (!row.ParentExists || row.End < row.Start || row.CheckOut != null && row.CheckIn == null
                || row.CheckOut < row.CheckIn || row.CheckIn < row.AssignedAt
                || row.CheckIn > now || row.CheckOut > now)
            { unresolved.Add("SHIFT_ATTENDANCE_DATA_INCONSISTENCY"); continue; }
            if (row.CheckIn != null && row.CheckOut == null)
            {
                if (row.AssignmentStatus == "ASSIGNED") blockers.Add("SHIFT_OPEN_ATTENDANCE");
                else unresolved.Add("SHIFT_OPEN_ATTENDANCE_MAPPING");
            }
        }
        // Task53 V1: exact designated-assignee evidence for stored nonterminal states.
        // This does not resolve Pending Verification/Closed/reopen, requester next actions,
        // or mandatory handover acknowledgment; retain the global contract gates above.
        var tasks = await (from assignment in db.OperationalTaskAssignments.AsNoTracking()
            where assignment.AssigneeId == targetId
            join parent in db.OperationalTasks.AsNoTracking() on assignment.TaskId equals parent.TaskId into parents
            from parent in parents.DefaultIfEmpty()
            select new { assignment.AssignmentStatus, assignment.AssignedAt, assignment.EndedAt,
                Parent = parent == null ? null : parent.Status }).ToArrayAsync(ct);
        foreach (var row in tasks)
        {
            if (row.AssignmentStatus is not ("ACTIVE" or "COMPLETED" or "REASSIGNED" or "CANCELLED")
                || row.Parent is not ("TODO" or "IN_PROGRESS" or "BLOCKED" or "COMPLETED" or "CANCELLED"))
            { unresolved.Add("OPERATIONAL_TASK_UNRESOLVED"); continue; }
            if (row.EndedAt < row.AssignedAt || row.AssignmentStatus == "ACTIVE" && row.EndedAt != null
                || row.AssignmentStatus != "ACTIVE" && row.EndedAt == null)
            { unresolved.Add("OPERATIONAL_TASK_DATA_INCONSISTENCY"); continue; }
            if (row.AssignmentStatus != "ACTIVE") continue;
            if (row.Parent is "TODO" or "IN_PROGRESS" or "BLOCKED") blockers.Add("OPERATIONAL_TASK_ASSIGNEE");
            else if (row.Parent == "COMPLETED") unresolved.Add("OPERATIONAL_TASK_TERMINAL_MAPPING");
            else unresolved.Add("OPERATIONAL_TASK_DATA_INCONSISTENCY");
        }
        var requests = await (from assignment in db.InternalRequestAssignments.AsNoTracking()
            where assignment.AssigneeId == targetId
            join parent in db.InternalRequests.AsNoTracking() on assignment.InternalRequestId equals parent.InternalRequestId into parents
            from parent in parents.DefaultIfEmpty()
            select new { assignment.Status, assignment.AssignedAt, assignment.EndedAt,
                Parent = parent == null ? null : parent.Status }).ToArrayAsync(ct);
        foreach (var row in requests)
        {
            if (row.Status is not ("ACTIVE" or "COMPLETED" or "REASSIGNED" or "CANCELLED")
                || row.Parent is not ("DRAFT" or "SUBMITTED" or "IN_PROGRESS" or "COMPLETED" or "CLOSED" or "REJECTED" or "CANCELLED"))
            { unresolved.Add("INTERNAL_REQUEST_UNRESOLVED"); continue; }
            if (row.EndedAt < row.AssignedAt || row.Status == "ACTIVE" && row.EndedAt != null
                || row.Status != "ACTIVE" && row.EndedAt == null)
            { unresolved.Add("INTERNAL_REQUEST_DATA_INCONSISTENCY"); continue; }
            if (row.Status != "ACTIVE") continue;
            if (row.Parent == "IN_PROGRESS") blockers.Add("INTERNAL_REQUEST_ASSIGNEE");
            else unresolved.Add("INTERNAL_REQUEST_NEXT_ACTION_MAPPING");
        }
        return new(blockers.Order(StringComparer.Ordinal).ToArray(), unresolved.Order(StringComparer.Ordinal).ToArray())
        {
            Unsupported = ["UNSCHEDULED_WORKDAY_MODEL", "MANDATORY_HANDOVER_ACKNOWLEDGMENT_MODEL"]
        };
    }
}
