using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

// Runs as system work, authorized by the series' still-eligible initiating manager.
// No fabricated session or permission bypass is used for scheduled assignments.
internal sealed class TaskSeriesGeneration(HotelOpsDbContext db, TimeProvider clock)
{
    internal async Task<long[]> PendingAsync(long after, int take, CancellationToken ct)
    {
        if (!await TaskSeriesStore.InstalledAsync(db, ct)) return [];
        var now = clock.GetUtcNow().UtcDateTime;
        var horizon = TaskRecurrenceSchedule.GenerationHorizon(now).ToDateTime(TimeOnly.MaxValue).AddHours(-7);
        return await TaskSeriesStore.Query(db).Where(s => s.SeriesId > after && s.IsActive && s.NextDueAt != null && s.NextDueAt <= horizon)
            .OrderBy(s => s.SeriesId).Take(take).Select(s => s.SeriesId).ToArrayAsync(ct);
    }

    internal async Task<int> GenerateAsync(long id, CancellationToken ct)
    {
        if (!await TaskSeriesStore.InstalledAsync(db, ct)) throw new ValidationException("Recurring task schema is unavailable.");
        var hint = await TaskSeriesStore.Query(db).SingleOrDefaultAsync(s => s.SeriesId == id, ct);
        if (hint == null) return 0;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var users = await new UserEligibilityLock(db).LockAsync([hint.CreatedBy, hint.DefaultAssigneeId], ct);
            UserEligibilityLock.RequireEligible(hint.CreatedBy, users);
            UserEligibilityLock.RequireEligible(hint.DefaultAssigneeId, users);
            var branch = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={hint.BranchId}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (branch?.IsActive != true) throw new ValidationException("Recurring branch is unavailable.");
            var acquired = await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={RoomTypeCreationService.CatalogLock},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @r AS Value;").ToArrayAsync(ct);
            if (acquired.Single() < 0) throw new TimeoutException("Recurring catalog lock unavailable.");
            await db.Database.SqlQuery<long>($"SELECT series_id AS Value FROM dbo.operational_task_series WITH(UPDLOCK,HOLDLOCK) WHERE series_id={id}").ToArrayAsync(ct);
            var row = await TaskSeriesStore.Query(db).SingleAsync(s => s.SeriesId == id, ct);
            if (row.CreatedBy != hint.CreatedBy || row.DefaultAssigneeId != hint.DefaultAssigneeId || row.BranchId != hint.BranchId)
                throw new ValidationException("Recurring responsibility changed.");
            var count = await MaterializeAsync(db, row, await SqlNowAsync(db, ct), ct, clock.GetUtcNow().UtcDateTime);
            await tx.CommitAsync(ct);
            return count;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally { db.ChangeTracker.Clear(); }
    }

    // Caller owns the same ordered user/branch/catalog/series locks and transaction.
    // A bounded batch prevents old series from monopolizing the worker; the next
    // pass continues from its durable next_due_at without repeating occurrences.
    internal static async Task<int> MaterializeAsync(HotelOpsDbContext db, StoredTaskSeries row, DateTime now, CancellationToken ct, DateTime? generationAsOf = null)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Recurring generation requires a transaction.");
        if (!row.IsActive || row.NextDueAt == null) return 0;
        // Clock injection controls only the scheduling horizon. Authorization and
        // persisted timestamps always use SQL time, including in trial simulations.
        var horizon = TaskRecurrenceSchedule.GenerationHorizon(generationAsOf ?? now);
        if (DateOnly.FromDateTime(TaskRecurrenceSchedule.Local(row.NextDueAt.Value)) > horizon) return 0;
        var template = TaskSeriesStore.Template(row);
        var rule = TaskSeriesStore.Rule(row);
        await RequireCurrentAuthorityAsync(db, row, template, now, ct);
        var count = 0;
        while (row.NextDueAt is { } due && DateOnly.FromDateTime(TaskRecurrenceSchedule.Local(due)) <= horizon && count < 100)
        {
            var date = DateOnly.FromDateTime(TaskRecurrenceSchedule.Local(due));
            var existing = await db.Database.SqlQuery<long>($"SELECT task_id AS Value FROM dbo.task_series_occurrences WHERE series_id={row.SeriesId} AND due_date={date}").ToArrayAsync(ct);
            // Link and cursor are atomic. A duplicate with an unadvanced cursor is
            // evidence of inconsistent storage, not permission to rewrite history.
            if (existing.Length != 0) throw new ValidationException("Recurring occurrence cursor is inconsistent.");
            var request = Occurrence(template, row.SeriesId, due);
            var task = await RoomTypeCreationService.WriteTaskCreationAsync(db, row.CreatedBy, row.BranchId, request, now, ct, row.SeriesId);
            await AssignAsync(db, row, task, now, ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_series_occurrences(series_id,due_date,due_at,task_id,created_at) VALUES({row.SeriesId},{date},{due},{task.TaskId},{now})", ct);
            var next = TaskRecurrenceSchedule.Next(due, rule);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_series SET next_due_at={next} WHERE series_id={row.SeriesId}", ct);
            db.AuditLogs.Add(new AuditLog { ActorUserId = null, BranchId = row.BranchId, EntityType = "operational_task_series", EntityId = row.SeriesId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Action = "CREATE", OccurredAt = now, NewValues = JsonSerializer.Serialize(new { operation = "GENERATE", job = "JOB05", taskId = task.TaskId, dueAt = due, authorizedBy = row.CreatedBy }),
                ChangedFields = "[\"occurrence\",\"nextDueAt\"]" });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            row.NextDueAt = next;
            count++;
        }
        await RequireCurrentAuthorityAsync(db, row, template, await SqlNowAsync(db, ct), ct);
        return count;
    }

    internal static CreateOperationalTaskRequest Occurrence(CreateOperationalTaskRequest template, long seriesId, DateTime due)
    {
        var date = DateOnly.FromDateTime(TaskRecurrenceSchedule.Local(due));
        var material = FormattableString.Invariant($"HotelOps:TaskSeries:v1:{seriesId}:{template.RequestKey:N}:{date:yyyy-MM-dd}");
        var key = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(material)).AsSpan(0, 16));
        var delta = due - template.DueAt!.Value.UtcDateTime;
        return template with { RequestKey = key, DueAt = new(DateTime.SpecifyKind(due, DateTimeKind.Utc)),
            PlannedStartAt = template.PlannedStartAt?.Add(delta) };
    }

    private static async Task RequireCurrentAuthorityAsync(HotelOpsDbContext db, StoredTaskSeries row, CreateOperationalTaskRequest template, DateTime now, CancellationToken ct)
    {
        if (!(await TaskSubmissionAccess.ManagersAsync(db, row.BranchId, now, ct)).Contains(row.CreatedBy)
            || !await TaskAssignmentEligibility.Candidates(db, row.BranchId, row.DepartmentId, now).AnyAsync(u => u.UserId == row.DefaultAssigneeId, ct)
            || !await RoomTypeCreationService.TaskCreationReferencesAsync(db, row.BranchId, template, ct))
            throw new ValidationException("Recurring authority, default executor or catalog is no longer eligible.");
    }

    private static async Task AssignAsync(HotelOpsDbContext db, StoredTaskSeries row, OperationalTask task, DateTime now, CancellationToken ct)
    {
        var assignment = new OperationalTaskAssignment { TaskId = task.TaskId, AssigneeId = row.DefaultAssigneeId, AssignedBy = row.CreatedBy, AssignedAt = now, AssignmentStatus = "ACTIVE" };
        var progress = new OperationalTaskProgress { TaskId = task.TaskId, ProgressPercent = 0, OldStatus = "TODO", NewStatus = "TODO", Description = "Task assigned.", ReportedBy = row.CreatedBy, ReportedAt = now };
        db.OperationalTaskAssignments.Add(assignment); db.OperationalTaskProgresses.Add(progress);
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        db.AuditLogs.Add(new AuditLog { ActorUserId = row.CreatedBy, BranchId = row.BranchId, EntityType = "operational_tasks", EntityId = task.TaskId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Action = "UPDATE", OccurredAt = now, OldValues = "{\"workflowState\":\"PENDING_ASSIGNMENT\",\"assigneeId\":null}",
            NewValues = JsonSerializer.Serialize(new { operation = "ASSIGN", workflowState = "ASSIGNED", assignmentId = assignment.AssignmentId,
                previousAssignmentId = (long?)null, previousAssigneeId = (long?)null, assigneeId = row.DefaultAssigneeId, progressId = progress.ProgressId,
                reason = (string?)null, closedIntervalId = (long?)null, recipients = new[] { row.DefaultAssigneeId }, notificationKind = "TASK_ASSIGNED", automation = new { job = "JOB05", seriesId = row.SeriesId } }),
            ChangedFields = "[\"workflowState\",\"assignment\",\"notificationIntent\"]" });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='ASSIGNED' WHERE task_id={task.TaskId}; UPDATE dbo.operational_tasks SET updated_at={now} WHERE task_id={task.TaskId}", ct);
    }

    private static Task<DateTime> SqlNowAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
}
