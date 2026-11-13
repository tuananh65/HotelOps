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
        public byte[] TaskRevision { get; set; } = [];
        public DateTime TaskCompletedAt { get; set; }
    }

    private async Task<bool> CleanerAttributionInstalled(CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_cleaner_attributions','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_capture_cleaner_attribution','P') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.triggers
                WHERE object_id=OBJECT_ID('dbo.TR_fe04_cleaner_immutable','TR')
                  AND parent_id=OBJECT_ID('dbo.fe04_cleaner_attributions','U') AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
                AND name='HotelOps.FE04.CleanerAttribution' AND CONVERT(nvarchar(128),value)='R04_V1')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    // Called by the production capture path. This is a policy boundary, not a SQL atomicity test seam.
    public static void RequireCleanerCapturePolicy(bool installed, bool uniqueCleaner, bool eligibleAtCompletion)
    {
        if (!installed) throw new InvalidOperationException("CLEANER_ATTRIBUTION_SCHEMA_REQUIRED");
        if (!uniqueCleaner) throw new InvalidOperationException("CLEANER_ATTRIBUTION_SOURCE_AMBIGUOUS");
        if (!eligibleAtCompletion) throw new InvalidOperationException("CLEANER_ATTRIBUTION_AS_OF_COMPLETION_AUTHORITY_REQUIRED");
    }

    private async Task CaptureCleanerAttribution(long task, long assignment, long actor, long progress, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not { } current)
            throw new InvalidOperationException("CLEANER_ATTRIBUTION_CALLER_TRANSACTION_REQUIRED");
        RequireAuthorityTransaction(db.Database.GetDbConnection(), current.GetDbTransaction());
        RequireCleanerCapturePolicy(await CleanerAttributionInstalled(ct), true, true);

        var assigned = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == task)
            .Select(a => a.AssigneeUserId).Distinct().ToArrayAsync(ct);
        var contributors = await db.CleaningProgressLogs.AsNoTracking().Where(p => p.CleaningTaskId == task)
            .Select(p => p.LoggedBy).Distinct().ToArrayAsync(ct);
        RequireCleanerCapturePolicy(true, CleanerAttributionContract.UniqueCleaner(actor, assigned, contributors), true);

        var completion = await db.CleaningTasks.AsNoTracking().Where(t => t.CleaningTaskId == task)
            .Select(t => new { t.RoomId, BranchId = t.Room.BranchId, t.ActualEndAt }).SingleOrDefaultAsync(ct);
        if (completion?.ActualEndAt is not { } completedAt)
            throw new InvalidOperationException("CLEANER_ATTRIBUTION_COMPLETED_SOURCE_REQUIRED");
        // This is checked while completion is being written, at its authoritative completion time.
        // Later employment/role state must not rewrite an immutable historical cleaner snapshot.
        var completionUtc = DateTime.SpecifyKind(completedAt, DateTimeKind.Utc);
        RequireCleanerCapturePolicy(true, true, await EligibleHousekeeper(actor, completion.BranchId, completionUtc, ct));

        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fe04_capture_cleaner_attribution @task={task}, @assignment={assignment}, @cleaner={actor}, @progress={progress}", ct);

        // An acknowledged EXEC is not sufficient: require the exact immutable persisted source.
        var captured = await db.Database.SqlQuery<CleanerAttributionRow>($"""
            SELECT a.cleaning_task_id AS CleaningTaskId,a.room_id AS RoomId,a.branch_id AS BranchId,
              a.assignment_id AS AssignmentId,a.cleaner_user_id AS CleanerUserId,
              a.completion_progress_id AS CompletionProgressId,a.completed_at AS CompletedAt,
              a.completion_revision AS CompletionRevision,a.writer_transaction_id AS WriterTransactionId,
              c.cleaning_task_id AS CurrentCycleId,t.row_version AS TaskRevision,
              t.actual_end_at AS TaskCompletedAt
            FROM dbo.fe04_cleaner_attributions a WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=a.cleaning_task_id
              AND t.room_id=a.room_id
            JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=a.room_id
            WHERE a.cleaning_task_id={task}
            """).SingleOrDefaultAsync(ct);
        var writerTransaction = await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS [Value]").SingleAsync(ct);
        if (captured == null || captured.RoomId != completion.RoomId || captured.BranchId != completion.BranchId
            || captured.AssignmentId != assignment || captured.CleanerUserId != actor
            || captured.CompletionProgressId != progress || captured.CompletedAt != completedAt
            || captured.TaskCompletedAt != completedAt || captured.WriterTransactionId != writerTransaction)
            throw new InvalidOperationException("CLEANER_ATTRIBUTION_CAPTURE_UNVERIFIED");
        var immutable = new CleanerAttribution(captured.CleaningTaskId, captured.RoomId, captured.BranchId,
            captured.AssignmentId, captured.CleanerUserId, captured.CompletionProgressId,
            DateTime.SpecifyKind(captured.CompletedAt, DateTimeKind.Utc), captured.CompletionRevision,
            captured.WriterTransactionId);
        if (!CleanerAttributionContract.Matches(immutable, task, completion.RoomId, completion.BranchId,
                captured.CurrentCycleId, captured.TaskRevision, true, true))
            throw new InvalidOperationException("CLEANER_ATTRIBUTION_CAPTURE_UNVERIFIED");
    }

    // FE05 must authorize its Inspection caller first, using its own scoped Serializable transaction.
    // Current employment/role state is NOT evidence of cleaner eligibility at completion time:
    // only the immutable producer-validated capture, source history and caller authorization can bind it.
    public async Task<CleanerAttribution?> ReadInspectionCleanerAttributionAsync(DbConnection connection,
        DbTransaction transaction, long inspection, long branch, CancellationToken ct)
    {
        RequireAuthorityTransaction(connection, transaction);
        if (inspection <= 0 || branch <= 0 || !await CleanerAttributionInstalled(ct)) return null;
        var row = await db.Database.SqlQuery<CleanerAttributionRow>($"""
            SELECT a.cleaning_task_id AS CleaningTaskId,a.room_id AS RoomId,a.branch_id AS BranchId,
              a.assignment_id AS AssignmentId,a.cleaner_user_id AS CleanerUserId,a.completion_progress_id AS CompletionProgressId,
              a.completed_at AS CompletedAt,a.completion_revision AS CompletionRevision,a.writer_transaction_id AS WriterTransactionId,
              c.cleaning_task_id AS CurrentCycleId,t.row_version AS TaskRevision,t.actual_end_at AS TaskCompletedAt
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
        if (row == null || row.CompletedAt != row.TaskCompletedAt) return null;
        var source = new CleanerAttribution(row.CleaningTaskId, row.RoomId, row.BranchId,
            row.AssignmentId, row.CleanerUserId, row.CompletionProgressId,
            DateTime.SpecifyKind(row.CompletedAt, DateTimeKind.Utc), row.CompletionRevision, row.WriterTransactionId);
        // The completion revision is immutable historical source, NOT the task's later live rowversion.
        return CleanerAttributionContract.Matches(source, row.CleaningTaskId, row.RoomId, branch,
            row.CurrentCycleId, row.CompletionRevision, true, true) ? source : null;
    }

    private async Task<bool> VerifyCompletedCleanerAttributionAsync(long task, long assignment, long actor,
        long progress, DateTime completedAtUtc, CancellationToken ct)
    {
        if (!await CleanerAttributionInstalled(ct)) return false;
        var row = await db.Database.SqlQuery<CleanerAttributionRow>($"""
            SELECT a.cleaning_task_id AS CleaningTaskId,a.room_id AS RoomId,a.branch_id AS BranchId,
              a.assignment_id AS AssignmentId,a.cleaner_user_id AS CleanerUserId,a.completion_progress_id AS CompletionProgressId,
              a.completed_at AS CompletedAt,a.completion_revision AS CompletionRevision,a.writer_transaction_id AS WriterTransactionId,
              c.cleaning_task_id AS CurrentCycleId,t.row_version AS TaskRevision,t.actual_end_at AS TaskCompletedAt
            FROM dbo.fe04_cleaner_attributions a WITH(HOLDLOCK)
            JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=a.cleaning_task_id AND t.room_id=a.room_id
            JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=a.room_id AND c.cleaning_task_id=a.cleaning_task_id
            WHERE a.cleaning_task_id={task}
            """).SingleOrDefaultAsync(ct);
        if (row == null || row.AssignmentId != assignment || row.CleanerUserId != actor
            || row.CompletionProgressId != progress || row.CompletedAt != completedAtUtc
            || row.TaskCompletedAt != completedAtUtc) return false;
        var source = new CleanerAttribution(row.CleaningTaskId, row.RoomId, row.BranchId, row.AssignmentId,
            row.CleanerUserId, row.CompletionProgressId, DateTime.SpecifyKind(row.CompletedAt, DateTimeKind.Utc),
            row.CompletionRevision, row.WriterTransactionId);
        return CleanerAttributionContract.Matches(source, task, row.RoomId, row.BranchId,
            row.CurrentCycleId, row.TaskRevision, true, true);
    }
}
