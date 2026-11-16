using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.WorkTime;

internal static class AssignedWorkdayBoundary
{
    // Physical IN_PROGRESS survives Pause/Hold/Submit. Only the existing workflow
    // authority can establish a stopped source; absence of an interval cannot.
    // Caller retains employee/time locks and the Serializable transaction until End.
    internal static async Task<bool> TasksStoppedAsync(HotelOpsDbContext db, long actor, CancellationToken ct)
    {
        RequireTransaction(db);
        if (!await db.OperationalTasks.AsNoTracking().AnyAsync(t => t.Status == "IN_PROGRESS"
            && t.OperationalTaskAssignments.Any(a => a.AssigneeId == actor && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null), ct)) return true;
        if (await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH('dbo.task_progress_versions','workflow_status') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct) != 1)
            return false;
        return !await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.operational_tasks t WITH(HOLDLOCK)
            LEFT JOIN dbo.task_progress_versions v WITH(HOLDLOCK) ON v.task_id=t.task_id
            WHERE t.status='IN_PROGRESS' AND EXISTS (
              SELECT 1 FROM dbo.operational_task_assignments a WITH(HOLDLOCK)
              WHERE a.task_id=t.task_id AND a.assignee_id={actor} AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL)
            AND (t.completed_at IS NOT NULL OR v.workflow_status IS NULL
              OR v.workflow_status NOT IN ('PAUSED','PENDING_VERIFICATION'))
            """).AnyAsync(ct);
    }

    internal static async Task<bool> CleaningStoppedAsync(HotelOpsDbContext db, long actor, CancellationToken ct)
    {
        RequireTransaction(db);
        if (!await db.CleaningTasks.AsNoTracking().AnyAsync(t => t.Status == "IN_PROGRESS"
            && t.CleaningAssignments.Any(a => a.AssigneeUserId == actor && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null), ct)) return true;
        if (await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct) != 1)
            return false;
        return !await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.cleaning_tasks t WITH(HOLDLOCK)
            WHERE t.status='IN_PROGRESS' AND EXISTS (
              SELECT 1 FROM dbo.cleaning_assignments a WITH(HOLDLOCK)
              WHERE a.cleaning_task_id=t.cleaning_task_id AND a.assignee_user_id={actor}
                AND a.assignment_status='ACTIVE' AND a.ended_at IS NULL)
            AND (t.workflow_status IS NULL OR NOT (
              (t.workflow_status='ON_HOLD' AND t.actual_end_at IS NULL)
              OR (t.workflow_status IN ('PENDING_INSPECTION','REWORK_REQUIRED') AND t.actual_end_at IS NOT NULL)))
            """).AnyAsync(ct);
    }

    private static void RequireTransaction(HotelOpsDbContext db)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new WorkTimeContractException("TIME_TRANSACTION_REQUIRED");
    }
}
