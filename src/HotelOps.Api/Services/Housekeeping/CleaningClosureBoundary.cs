using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ReportCleaningBlockerAsync(SessionReference ticket, long sid,
        long branch, long task, string tag, string key, ReportCleaningBlockerRequest input, CancellationToken ct, string? privateRoot = null)
    {
        ValidateCleaningClosure(branch, task, tag, key, input.Note, input.Photos);
        if (input.ReasonId <= 0) throw new ValidationException("A positive blocker reference is required.");
        return ExecuteCleaningClosureAsync(ticket, sid, branch, task, tag, key, input.ReasonId,
            CleaningLifecycleRules.Reason(input.Note, 1), null, input.Photos, privateRoot, ct);
    }

    public Task<RoomTypeBatchResult> CompleteCleaningAsync(SessionReference ticket, long sid,
        long branch, long task, string tag, string key, CompleteCleaningRequest input, CancellationToken ct, string? privateRoot = null)
    {
        ValidateCleaningClosure(branch, task, tag, key, input.Note, input.Photos);
        return ExecuteCleaningClosureAsync(ticket, sid, branch, task, tag, key, null,
            CleaningLifecycleRules.Reason(input.Note, 1), input.ProgressETag, input.Photos, privateRoot, ct);
    }

    private static void ValidateCleaningClosure(long branch, long task, string tag, string key, string? note, IReadOnlyList<EvidenceUpload>? photos)
    {
        if (branch <= 0 || task <= 0 || !CleaningLifecycleRules.StrongTag(tag))
            throw new ValidationException("Positive branch/task and a strong task ETag are required.");
        CleaningLifecycleRules.Operation(key);
        CleaningLifecycleRules.Reason(note, 1);
        if (photos?.Count > 5 || photos?.Any(p => p == null) == true) throw new ValidationException("At most five evidence files are allowed.");
    }

    public static byte[] ClosureFingerprint(long actor, long branch, long task, string tag, long? reason,
        string note, string? progressTag, IReadOnlyList<EvidenceUpload>? photos)
        => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { version = 1, actor, branch, task, tag,
            action = reason == null ? "COMPLETE" : "BLOCKER", reason, note = note.Trim(), progressTag,
            photos = (photos ?? []).Select(p => new { p.Name, p.MimeType, digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(p.Base64 ?? ""))) }) }));

    public static bool ValidClosureReceipt(CleaningClosureResult value, long branch, long task, long assignment, Guid operation, bool complete)
        => value.Id == task && value.BranchId == branch && value.AssignmentId == assignment && value.OperationId == operation
        && value.PublicationId != Guid.Empty && value.ProgressId > 0 && value.ClosedIntervalId > 0
        && value.OccurredAtUtc.Kind == DateTimeKind.Utc && CleaningLifecycleRules.StrongTag(value.SourceETag)
        && value.State == (complete ? "PENDING_INSPECTION" : "ON_HOLD")
        && value.Readiness == (complete ? "PENDING_INSPECTION" : "PENDING_CLEANING")
        && value.Notification == "PENDING_DELIVERY" && value.Contract == "PROVISIONAL_NOT_ACCEPTED";

    // This is a structural guard, not an authorization or completed-ledger substitute.
    public static CleaningClosureResult? ReadClosureReceipt(
        HotelOps.Api.Models.NotificationOperationCandidate stored, long actor, byte[] expectedFingerprint,
        long branch, long task, long assignment, Guid operation, bool complete)
    {
        if (actor <= 0 || branch <= 0 || task <= 0 || assignment <= 0 || operation == Guid.Empty
            || expectedFingerprint is not { Length: 32 } || stored.ActorUserId != actor
            || stored.OperationKey != operation || stored.FingerprintVersion != 1 || !stored.Completed
            || stored.Fingerprint is not { Length: 32 }
            || !CryptographicOperations.FixedTimeEquals(stored.Fingerprint, expectedFingerprint)
            || string.IsNullOrWhiteSpace(stored.ResultJson)) return null;
        try
        {
            var receipt = JsonSerializer.Deserialize<CleaningClosureResult>(stored.ResultJson, Json);
            return receipt != null && ValidClosureReceipt(receipt, branch, task, assignment, operation, complete)
                ? receipt : null;
        }
        catch (JsonException) { return null; }
    }

    private async Task<RoomTypeBatchResult> ExecuteCleaningClosureAsync(SessionReference ticket, long sid, long branch,
        long task, string tag, string key, long? reason, string note, string? progressTag,
        IReadOnlyList<EvidenceUpload>? photos, string? root, CancellationToken ct)
    {
        var operation = CleaningLifecycleRules.Operation(key);
        var fingerprint = ClosureFingerprint(ticket.UserId, branch, task, tag, reason, note, progressTag, photos);
        var files = new List<string>();
        // On ambiguous commit outcome keep files; committed attachment rows may reference them.
        var commitAttempted = false;
        try { return await Core(false, ct); }
        catch (WorkTimeContractException error) { return LifecycleTimeFailure(error); }
        catch (Exception error) when (error is System.Data.Common.DbException or DbUpdateException or OperationCanceledException)
        {
            if (!commitAttempted) { foreach (var file in files) File.Delete(file); files.Clear(); }
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { return await Core(true, budget.Token); }
            catch (Exception recovery) when (recovery is System.Data.Common.DbException or DbUpdateException or OperationCanceledException or WorkTimeContractException)
            { return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "CLOSURE_OUTCOME_UNCONFIRMED"); }
        }
        finally { if (!commitAttempted) foreach (var file in files) File.Delete(file); }

        async Task<RoomTypeBatchResult> Core(bool recoveryOnly, CancellationToken token)
        {
            var enabled = candidateNotification != null && await candidateNotification.ClosureAvailableAsync(token);
            var recipients = enabled ? await candidateNotification!.ClosureRecipientsAsync(branch, token) : [];
            var users = recipients.Append(ticket.UserId).Distinct().Order().ToArray();
            var time = new SqlWorkTimeTransactionParticipant(db);
            var mutation = false;
            return await BatchAsync(ticket, sid, branch, async (actor, inner) =>
            {
                if (!await CleaningExecutionInstalled(inner) || !await LifecycleInstalled(inner)) return new(RoomTypeBatchStatus.SchemaRequired);
                var row = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(x => x.CleaningTaskId == task
                    && x.Room.BranchId == branch && x.Room.IsActive && x.Room.Floor.IsActive
                    && (x.Room.AreaId == null || x.Room.Area!.IsActive), inner);
                if (row == null) return new(RoomTypeBatchStatus.NotFound);
                var owners = await db.CleaningAssignments.AsNoTracking().Where(x => x.CleaningTaskId == task
                    && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null).OrderBy(x => x.AssignmentId).Take(2).ToArrayAsync(inner);
                if (owners.Length != 1) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_ASSIGNMENT_REQUIRED");
                var owner = owners[0];
                if (owner.AssigneeUserId != actor) return LifecycleFailure(RoomTypeBatchStatus.Forbidden, "CURRENT_ASSIGNEE_REQUIRED");
                if (owner.AssignedAt > clock.GetUtcNow().UtcDateTime) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_ASSIGNMENT_REQUIRED");
                var state = await CleaningState(task, inner);
                if (!enabled)
                {
                    if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
                    if (state.State != "IN_PROGRESS") return LifecycleFailure(RoomTypeBatchStatus.Inactive, "TASK_NOT_IN_PROGRESS");
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "CLEANING_CLOSURE_CONTRACT_REQUIRED");
                }
                var stored = await candidateNotification!.FindAsync(actor, operation, inner);
                if (stored != null)
                {
                    if (stored.Fingerprint is not { Length: 32 }
                        || !CryptographicOperations.FixedTimeEquals(stored.Fingerprint, fingerprint))
                        return new(RoomTypeBatchStatus.Duplicate);
                    var receipt = ReadClosureReceipt(stored, actor, fingerprint, branch, task,
                        owner.AssignmentId, operation, reason == null);
                    if (receipt == null || receipt.SourceETag != WorkflowTag(state.Revision)
                        || state.State != (reason == null ? "PENDING_INSPECTION" : "ON_HOLD")
                        || reason == null && (row.ActualEndAt != receipt.OccurredAtUtc
                            || !await VerifyCompletedCleanerAttributionAsync(task, owner.AssignmentId, actor,
                                receipt.ProgressId, receipt.OccurredAtUtc, inner)))
                        return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "DURABLE_RESULT_CONTRACT_INVALID");
                    var originalRecipients = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WHERE publication_id={receipt.PublicationId}").SingleAsync(inner);
                    if (originalRecipients == 0) return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "DURABLE_STAGING_REQUIRED");
                    await candidateNotification.VerifyClosureAsync(actor, operation, originalRecipients, inner);
                    return new(RoomTypeBatchStatus.Success, receipt, receipt.SourceETag);
                }
                if (recoveryOnly) return new(RoomTypeBatchStatus.Unavailable);
                if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
                if (state.State != "IN_PROGRESS" || row.Status != "IN_PROGRESS" || row.ActualStartAt == null || row.ActualEndAt != null)
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive, "TASK_NOT_IN_PROGRESS");
                if (!await ResumeTrialInstalled(inner) || !await ProgressSchema.InstalledAsync(db, inner)) return new(RoomTypeBatchStatus.SchemaRequired);
                if (await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId}").SingleOrDefaultAsync(inner) != task)
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_CYCLE_REQUIRED");
                if (recipients.Length == 0 || !(await candidateNotification.ClosureRecipientsAsync(branch, inner)).SequenceEqual(recipients))
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "AUTHORIZED_SUPERVISOR_REQUIRED");
                if (reason is { } reference && !await db.ReferenceValues.AsNoTracking().AnyAsync(x => x.ReferenceId == reference
                    && x.GroupCode == "BLOCKER" && x.IsActive, inner)) return new(RoomTypeBatchStatus.InvalidReference);
                var blockers = await db.Database.SqlQuery<int>($"""
                    SELECT CASE WHEN b.progress_id IS NULL THEN 2 WHEN b.incident_id IS NOT NULL OR b.material_request_id IS NOT NULL THEN 3
                    WHEN b.resolved_at IS NULL THEN 1 ELSE 0 END AS [Value]
                    FROM dbo.cleaning_progress_logs p LEFT JOIN dbo.fs014_cleaning_blocker_resolutions b ON b.progress_id=p.progress_id
                    WHERE p.cleaning_task_id={task} AND p.status='BLOCKED'
                    """).ToArrayAsync(inner);
                if (blockers.Contains(2)) return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "BLOCKER_STATE_UNRECONCILED");
                if (blockers.Contains(3)) return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "BLOCKER_SOURCE_CONFIRMATION_REQUIRED");
                if (blockers.Contains(1)) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "ACTIVE_BLOCKER");
                var revision = await ProgressRevision(true, task, inner);
                if (revision == null) return new(RoomTypeBatchStatus.SchemaRequired);
                if (reason == null && (!CleaningLifecycleRules.StrongTag(progressTag) || progressTag != WorkflowTag(revision.Revision)))
                    return new(RoomTypeBatchStatus.Stale);
                var progress = await ProgressDto(true, task, branch, state.State!, inner);
                if (progress.Items.Count == 0 || reason == null && progress.Items.Any(i => i.Required && i.Done != true))
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive, "REQUIRED_ITEMS_INCOMPLETE");
                var open = await db.WorkIntervals.AsNoTracking().Where(w => w.WorkItemKind == "CLEANING" && w.WorkItemId == task && w.ClosedAt == null)
                    .OrderBy(w => w.IntervalId).Take(2).ToArrayAsync(inner);
                if (open.Length != 1 || open[0].UserId != actor || open[0].BranchId != branch)
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_WORK_INTERVAL_REQUIRED");
                var closed = await time.CloseIntervalAsync(new(actor, actor, branch, "CLEANING", task), open[0].IntervalId, open[0].RowVersion, inner);
                db.ChangeTracker.Clear();
                var now = closed.ClosedAtUtc!.Value;
                var next = reason == null ? "PENDING_INSPECTION" : "ON_HOLD";
                var log = new CleaningProgressLog { CleaningTaskId = task, ProgressPercent = reason == null ? 100 : progress.Percent,
                    Status = reason == null ? "COMPLETED" : "BLOCKED", BlockerTypeId = reason, Note = note, LoggedBy = actor, LoggedAt = now };
                db.Add(log); await db.SaveChangesAsync(inner); db.ChangeTracker.Clear();
                if (reason != null) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.fs014_cleaning_blocker_resolutions(progress_id) VALUES({log.ProgressId})", inner);
                foreach (var photo in photos ?? [])
                {
                    if (root == null) throw new ValidationException("Private evidence storage required.");
                    var attachment = await StoreFeatureEvidence("CLEANING_TASK", task, actor, photo, root, files, inner);
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.progress_evidence_links(attachment_id,cleaning_progress_id) VALUES({attachment},{log.ProgressId})", inner);
                }
                if (reason == null)
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='PENDING_INSPECTION',actual_end_at={now},completion_note={note} WHERE cleaning_task_id={task}", inner);
                else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='ON_HOLD' WHERE cleaning_task_id={task}", inner);
                // Mandatory immutable capture is part of this caller-owned completion transaction.
                // Missing schema, ambiguous history or failed verification aborts the entire write.
                if (reason == null) await CaptureCleanerAttribution(task, owner.AssignmentId, actor, log.ProgressId, inner);
                var blocked = await db.RoomBlocks.AsNoTracking().AnyAsync(x => x.RoomId == row.RoomId && x.ReleasedAt == null, inner);
                var readiness = reason == null ? "PENDING_INSPECTION" : "PENDING_CLEANING";
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at) VALUES({row.RoomId},{task},{readiness},{blocked},{actor},{now})", inner);
                var result = new CleaningClosureResult(task, branch, owner.AssignmentId, operation, Guid.NewGuid(), log.ProgressId,
                    closed.IntervalId, next, now, readiness, blocked, WorkflowTag((await CleaningState(task, inner)).Revision));
                await BatchAuditAsync(actor, branch, "cleaning_tasks", task, new { state.State, row.ActualEndAt },
                    new { operationId = operation, action = reason == null ? "COMPLETE_CLEANING" : "REPORT_CLEANING_BLOCKER", note,
                        reasonId = reason, progressId = log.ProgressId, entity = result }, ["workflowStatus", "workInterval", "readiness", "notification", "progress"], inner);
                await candidateNotification.StageClosureAsync(actor, fingerprint, row.RoomId, recipients, result, inner);
                mutation = true;
                return new(RoomTypeBatchStatus.Success, result, result.SourceETag);
            }, token, requireRoomTypeSchema: false, additionalUsers: users, preserveCommitErrors: true,
                featurePermission: inner => CleaningExecutionAccess(ticket.UserId, branch, task, "CLEANING_EXECUTE", inner),
                afterUserLocks: enabled && !recoveryOnly ? inner => time.LockUsersAsync(users, inner) : null,
                beforeCommit: async inner =>
                {
                    if (mutation)
                    {
                        if (!await candidateNotification!.ClosureAvailableAsync(inner) || !(await candidateNotification.ClosureRecipientsAsync(branch, inner)).SequenceEqual(recipients))
                            throw new InvalidOperationException("SUPERVISOR_SCOPE_CHANGED");
                        await candidateNotification.VerifyClosureAsync(ticket.UserId, operation, recipients.Length, inner);
                    }
                    commitAttempted = true;
                });
        }
    }
}
