using System.Data.Common;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private sealed class CleanerAttributionRow
    {
        public long CleaningTaskId { get; set; }
        public long RoomId { get; set; }
        public long BranchId { get; set; }
        public long AssignmentId { get; set; }
        public long CleanerUserId { get; set; }
        public long CompletionProgressId { get; set; }
        public DateTime CompletedAt { get; set; }
        public byte[] CompletionRevision { get; set; } = [];
        public long WriterTransactionId { get; set; }
        public long CurrentCycleId { get; set; }
    }

    private async Task<bool> CleanerAttributionInstalled(CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_cleaner_attributions','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_capture_cleaner_attribution','P') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                AND name='HotelOps.FE04.CleanerAttribution' AND CONVERT(nvarchar(128),value)='R04_V1')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private async Task CaptureCleanerAttribution(long task, long assignment, long actor, long progress, CancellationToken ct)
    {
        // Older installations continue to complete cleaning, but supply no attribution authority.
        if (!await CleanerAttributionInstalled(ct)) return;
        RequireAuthorityTransaction(db.Database.GetDbConnection(), db.Database.CurrentTransaction!.GetDbTransaction());
        var assigned = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == task)
            .Select(a => a.AssigneeUserId).Distinct().ToArrayAsync(ct);
        var contributors = await db.CleaningProgressLogs.AsNoTracking().Where(p => p.CleaningTaskId == task)
            .Select(p => p.LoggedBy).Distinct().ToArrayAsync(ct);
        if (!CleanerAttributionContract.UniqueCleaner(actor, assigned, contributors)) return;
        if (!await EligibleHousekeeper(actor, await db.CleaningTasks.Where(t => t.CleaningTaskId == task)
            .Select(t => t.Room.BranchId).SingleAsync(ct), clock.GetUtcNow().UtcDateTime, ct))
            throw new InvalidOperationException("CLEANER_ATTRIBUTION_ELIGIBILITY_CHANGED");
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fe04_capture_cleaner_attribution @task={task}, @assignment={assignment}, @cleaner={actor}, @progress={progress}", ct);
    }

    // Trusted FE05 caller must authorize the inspection operation before calling. No HTTP binding,
    // no latest assignment fallback, and no promotion of this result to UC035/036 authority.
    public async Task<CleanerAttribution?> ReadInspectionCleanerAttributionAsync(DbConnection connection,
        DbTransaction transaction, long inspection, long branch, CancellationToken ct)
    {
        RequireAuthorityTransaction(connection, transaction);
        if (inspection <= 0 || branch <= 0 || !await CleanerAttributionInstalled(ct)) return null;
        var row = await db.Database.SqlQuery<CleanerAttributionRow>($"""
            SELECT a.cleaning_task_id AS CleaningTaskId,a.room_id AS RoomId,a.branch_id AS BranchId,
              a.assignment_id AS AssignmentId,a.cleaner_user_id AS CleanerUserId,a.completion_progress_id AS CompletionProgressId,
              a.completed_at AS CompletedAt,a.completion_revision AS CompletionRevision,a.writer_transaction_id AS WriterTransactionId,
              c.cleaning_task_id AS CurrentCycleId
            FROM dbo.room_inspections i WITH(HOLDLOCK)
            JOIN dbo.fe04_cleaner_attributions a WITH(HOLDLOCK) ON a.cleaning_task_id=i.cleaning_task_id AND a.room_id=i.room_id
            JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=a.cleaning_task_id AND t.room_id=a.room_id AND t.actual_end_at=a.completed_at
            JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=a.room_id AND r.branch_id=a.branch_id AND r.is_active=1
            JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=a.room_id AND c.cleaning_task_id=a.cleaning_task_id
            JOIN dbo.cleaning_assignments ca WITH(HOLDLOCK) ON ca.assignment_id=a.assignment_id
              AND ca.cleaning_task_id=a.cleaning_task_id AND ca.assignee_user_id=a.cleaner_user_id
              AND ca.assigned_at<=a.completed_at AND (ca.ended_at IS NULL OR ca.ended_at>=a.completed_at)
            JOIN dbo.cleaning_progress_logs p WITH(HOLDLOCK) ON p.progress_id=a.completion_progress_id
              AND p.cleaning_task_id=a.cleaning_task_id AND p.logged_by=a.cleaner_user_id
              AND p.status='COMPLETED' AND p.progress_percent=100 AND p.logged_at=a.completed_at
            WHERE i.inspection_id={inspection} AND a.branch_id={branch} AND i.created_at>=a.completed_at
            """).SingleOrDefaultAsync(ct);
        if (row == null || !await EligibleHousekeeper(row.CleanerUserId, branch, clock.GetUtcNow().UtcDateTime, ct)) return null;
        var source = new CleanerAttribution(row.CleaningTaskId,row.RoomId,row.BranchId,row.AssignmentId,row.CleanerUserId,row.CompletionProgressId,
            DateTime.SpecifyKind(row.CompletedAt,DateTimeKind.Utc),row.CompletionRevision,row.WriterTransactionId);
        return CleanerAttributionContract.Matches(source,row.CleaningTaskId,row.RoomId,branch,row.CurrentCycleId,
            row.CompletionRevision,true,true) ? source : null;
    }
}
