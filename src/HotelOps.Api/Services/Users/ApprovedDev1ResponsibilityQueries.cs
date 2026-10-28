using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal sealed record Dev1ResponsibilityReview(IReadOnlyList<string> Blockers, IReadOnlyList<string> Unresolved);

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
            "LINE_MANAGER_CONTRACT", "DEV1_WRITER_COORDINATION"
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
        return new(blockers.Order(StringComparer.Ordinal).ToArray(), unresolved.Order(StringComparer.Ordinal).ToArray());
    }
}
