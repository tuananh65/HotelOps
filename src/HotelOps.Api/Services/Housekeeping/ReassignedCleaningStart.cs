using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // Only a committed reassignment of this exact current assignment can restart an
    // already-started task. Preserve the original actual start and all prior effort.
    private async Task<bool> ReassignedCleaningStartAllowed(long task, long branch, long assignment, DateTime firstStart, CancellationToken ct)
    {
        if (!await HotelOps.Api.Data.CleaningReassignmentSchema.InstalledAsync(db, ct)) return false;
        if (await db.WorkIntervals.AsNoTracking().AnyAsync(w => w.WorkItemKind == "CLEANING" && w.WorkItemId == task && w.ClosedAt == null, ct)) return false;
        var proofs = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.fs013_reassignment_operations o WITH(HOLDLOCK)
            JOIN dbo.cleaning_assignments a ON a.assignment_id=o.assignment_id
            JOIN dbo.cleaning_assignments p ON p.assignment_id=o.previous_assignment_id
            WHERE o.task_id={task} AND o.branch_id={branch} AND o.assignment_id={assignment}
              AND a.cleaning_task_id=o.task_id AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL
              AND a.assigned_by=o.actor_user_id AND a.assigned_at=o.occurred_at
              AND p.cleaning_task_id=o.task_id AND p.assignment_status='REASSIGNED' AND p.ended_at=o.occurred_at
              AND p.assignee_user_id<>a.assignee_user_id AND o.occurred_at>={firstStart}
              AND EXISTS(SELECT 1 FROM dbo.work_intervals w WHERE w.work_item_kind='CLEANING'
                AND w.work_item_id=o.task_id AND w.closed_at IS NOT NULL AND w.opened_at>={firstStart}
                AND w.closed_at<=o.occurred_at)
              AND EXISTS(SELECT 1 FROM dbo.audit_logs l WHERE l.entity_type='cleaning_tasks'
                AND l.entity_id=CONVERT(nvarchar(30),o.task_id) AND l.actor_user_id=o.actor_user_id
                AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(l.new_values,'$.operationId'))=o.operation_key
                AND TRY_CONVERT(bigint,JSON_VALUE(l.new_values,'$.assignmentId'))=o.assignment_id)
            """).ToArrayAsync(ct);
        if (proofs.Length != 1) return false;
        if (!await db.CleaningProgressLogs.AsNoTracking().AnyAsync(p => p.CleaningTaskId == task && p.Status == "BLOCKED", ct)) return true;
        if (!await ResumeTrialInstalled(ct)) return false;
        // Reassignment is not blocker resolution. Missing or linked confirmation stays closed.
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.cleaning_progress_logs p WITH(HOLDLOCK)
            LEFT JOIN dbo.fs014_cleaning_blocker_resolutions b WITH(HOLDLOCK) ON b.progress_id=p.progress_id
            WHERE p.cleaning_task_id={task} AND p.status='BLOCKED'
              AND (b.progress_id IS NULL OR b.resolved_at IS NULL OR b.incident_id IS NOT NULL OR b.material_request_id IS NOT NULL)
            """).SingleAsync(ct) == 0;
    }
}
