using HotelOps.Api.Data;
using HotelOps.Api.Services.OperationalTasks;
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
        // Current task projection/closure, assignment vocabulary and shift-state checks
        // below replace the old module-wide handoff placeholders. Missing or inconsistent
        // target rows still fail closed. Workday coverage is supplied by the integrated reader.
        var unresolved = new HashSet<string>(StringComparer.Ordinal) { "WORKDAY_CONTRACT" };
        // Read all designated references, including history and contradictory legacy rows.
        // Materialize only lifecycle scalars; classify exact stored vocabulary ordinally.
        var assignments = await (from assignment in db.CleaningAssignments.AsNoTracking()
            where assignment.AssigneeUserId == targetId
            join parent in db.CleaningTasks.AsNoTracking() on assignment.CleaningTaskId equals parent.CleaningTaskId into parents
            from parent in parents.DefaultIfEmpty()
            select new { assignment.CleaningTaskId, assignment.AssignmentStatus, assignment.AssignedAt, assignment.EndedAt,
                Parent = parent == null ? null : parent.Status }).ToArrayAsync(ct);
        var completedCleaning = new HashSet<long>();
        if (assignments.Any(a => a.AssignmentStatus == "ACTIVE" && a.Parent == "COMPLETED"))
        {
            if (await HousekeepingSchema.InstalledAsync(db, ct))
                completedCleaning = (await db.Database.SqlQuery<long>($"""
                    SELECT t.cleaning_task_id AS Value
                    FROM dbo.cleaning_tasks t WITH(HOLDLOCK)
                    WHERE t.status='COMPLETED' AND t.workflow_status='COMPLETED'
                      AND EXISTS(SELECT 1 FROM dbo.cleaning_assignments a WITH(HOLDLOCK)
                        WHERE a.cleaning_task_id=t.cleaning_task_id AND a.assignee_user_id={targetId}
                          AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL)
                      AND EXISTS(SELECT 1 FROM dbo.room_inspections i WITH(HOLDLOCK)
                        WHERE i.cleaning_task_id=t.cleaning_task_id AND i.room_id=t.room_id
                          AND i.workflow_status='PASSED' AND i.status='COMPLETED' AND i.result='PASS'
                          AND i.inspected_at IS NOT NULL
                          AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections later WITH(HOLDLOCK)
                            WHERE later.cleaning_task_id=t.cleaning_task_id
                              AND later.inspection_id>i.inspection_id))
                      AND NOT EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                        WHERE w.cleaning_task_id=t.cleaning_task_id
                          AND (w.workflow_status IS NULL OR w.workflow_status NOT IN('COMPLETED','FAILED','CANCELLED')))
                    """).ToArrayAsync(ct)).ToHashSet();
            else unresolved.Add("CLEANING_WORKFLOW_REQUIRED");
        }
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
            // Physical COMPLETED alone is insufficient. A current Passed lifecycle ends
            // the responsibility even though the historical cleaning assignment stays ACTIVE.
            // Room blocks affect readiness, not the already completed cleaner's assignment.
            else if (!completedCleaning.Contains(row.CleaningTaskId)) unresolved.Add("CLEANING_TERMINAL_MAPPING");
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
        // Unknown employment values remain a row-level blocker; an account with no
        // subordinate references does not require a hypothetical transfer contract.
        var subordinates = await db.Users.AsNoTracking().Where(x => x.ManagerUserId == targetId)
            .Select(x => new { x.DeletedAt, x.EmploymentStatus }).ToArrayAsync(ct);
        foreach (var row in subordinates)
        {
            if (row.DeletedAt != null) continue;
            // Classify ordinally: CI SQL matching must not certify unknown casing.
            if (row.EmploymentStatus == "ACTIVE") blockers.Add("LINE_MANAGER_DESIGNATION");
            else unresolved.Add("LINE_MANAGER_EMPLOYMENT_MAPPING");
        }
        // Future/current assignments remain responsibilities even before check-in.
        // A past scheduled end alone is not evidence that attendance or handover ended.
        var shifts = await (from assignment in db.ShiftAssignments.AsNoTracking()
            where assignment.UserId == targetId
            join shift in db.WorkShifts.AsNoTracking() on assignment.ShiftId equals shift.ShiftId into parents
            from shift in parents.DefaultIfEmpty()
            join attendance in db.ShiftAttendances.AsNoTracking() on assignment.ShiftAssignmentId equals attendance.ShiftAssignmentId into attendances
            from attendance in attendances.DefaultIfEmpty()
            select new { assignment.AssignmentStatus, assignment.AssignedAt,
                ParentExists = shift != null,
                ShiftStatus = shift == null ? null : shift.Status,
                Start = shift == null ? (DateTime?)null : shift.ScheduledStartAt,
                End = shift == null ? (DateTime?)null : shift.ScheduledEndAt,
                CheckIn = attendance == null ? (DateTime?)null : attendance.ActualCheckInAt,
                CheckOut = attendance == null ? (DateTime?)null : attendance.ActualCheckOutAt }).ToArrayAsync(ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        foreach (var row in shifts)
        {
            if (row.AssignmentStatus is not ("ASSIGNED" or "CANCELLED")
                || row.ShiftStatus is not ("PLANNED" or "CANCELLED"))
            { unresolved.Add("SHIFT_STATE_UNRESOLVED"); continue; }
            if (!row.ParentExists || row.End <= row.Start || row.AssignedAt > now || row.CheckOut != null && row.CheckIn == null
                || row.CheckOut < row.CheckIn || row.CheckIn < row.AssignedAt
                || row.CheckIn > now || row.CheckOut > now)
            { unresolved.Add("SHIFT_ATTENDANCE_DATA_INCONSISTENCY"); continue; }
            if (row.AssignmentStatus == "ASSIGNED" && row.ShiftStatus == "PLANNED" && row.End > now)
                blockers.Add("SHIFT_SCHEDULED_ASSIGNMENT");
            if (row.AssignmentStatus == "CANCELLED" && row.CheckIn != null)
                unresolved.Add("SHIFT_CANCELLED_ATTENDANCE_CONFLICT");
            if (row.CheckIn != null && row.CheckOut == null)
            {
                if (row.AssignmentStatus == "ASSIGNED") blockers.Add("SHIFT_OPEN_ATTENDANCE");
                else unresolved.Add("SHIFT_OPEN_ATTENDANCE_MAPPING");
            }
        }
        // Read the workflow projection and immutable closure proof used by task mutations.
        // Requester next actions and mandatory handover remain separate coverage gates.
        var tasks = await (from assignment in db.OperationalTaskAssignments.AsNoTracking()
            where assignment.AssigneeId == targetId
            join parent in db.OperationalTasks.AsNoTracking() on assignment.TaskId equals parent.TaskId into parents
            from parent in parents.DefaultIfEmpty()
            select new { assignment.TaskId, assignment.AssignmentStatus, assignment.AssignedAt, assignment.EndedAt,
                CompletedAt = parent == null ? null : parent.CompletedAt,
                Parent = parent == null ? null : parent.Status }).ToArrayAsync(ct);
        var taskIds = tasks.Select(t => t.TaskId).Distinct().ToArray();
        var taskStates = new Dictionary<long, string>();
        var closedTasks = new HashSet<long>();
        if (taskIds.Length > 0)
        {
            if (await ProgressSchema.InstalledAsync(db, ct))
                taskStates = await TaskWorkflowRead.Query(db, now).Where(t => taskIds.Contains(t.TaskId))
                    .ToDictionaryAsync(t => t.TaskId, t => t.State, ct);
            else unresolved.Add("OPERATIONAL_TASK_WORKFLOW_REQUIRED");
            if (tasks.Any(t => t.AssignmentStatus == "COMPLETED" && t.Parent == "COMPLETED" && t.EndedAt == t.CompletedAt))
            {
                if (await TaskVerificationStore.InstalledAsync(db, ct))
                    closedTasks = (await TaskVerificationStore.ProvenClosedTaskIds(db, now)
                        .Where(id => taskIds.Contains(id)).ToArrayAsync(ct)).ToHashSet();
                else unresolved.Add("OPERATIONAL_TASK_CLOSURE_EVIDENCE_REQUIRED");
            }
        }
        foreach (var row in tasks)
        {
            if (row.AssignmentStatus is not ("ACTIVE" or "COMPLETED" or "REASSIGNED" or "CANCELLED")
                || row.Parent is not ("TODO" or "IN_PROGRESS" or "BLOCKED" or "COMPLETED" or "CANCELLED"))
            { unresolved.Add("OPERATIONAL_TASK_UNRESOLVED"); continue; }
            if (row.AssignedAt > now || row.EndedAt > now || row.EndedAt < row.AssignedAt || row.AssignmentStatus == "ACTIVE" && row.EndedAt != null
                || row.AssignmentStatus != "ACTIVE" && row.EndedAt == null)
            { unresolved.Add("OPERATIONAL_TASK_DATA_INCONSISTENCY"); continue; }
            if (row.AssignmentStatus != "ACTIVE")
            {
                // Historical assignments in earlier cycles do not prove or invalidate today's closure.
                if (row.AssignmentStatus == "COMPLETED" && row.Parent == "COMPLETED" && row.EndedAt == row.CompletedAt
                    && !closedTasks.Contains(row.TaskId)) unresolved.Add("OPERATIONAL_TASK_CLOSURE_EVIDENCE_REQUIRED");
                continue;
            }
            if (!taskStates.TryGetValue(row.TaskId, out var state)
                || state is not ("ASSIGNED" or "ACCEPTED" or "IN_PROGRESS" or "PAUSED" or "PENDING_VERIFICATION"))
                unresolved.Add("OPERATIONAL_TASK_WORKFLOW_INCONSISTENCY");
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
                || row.Parent is not ("DRAFT" or "PENDING_ACCEPTANCE" or "ACCEPTED" or "IN_PROGRESS" or "CLOSED" or "REJECTED" or "CANCELLED"))
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
