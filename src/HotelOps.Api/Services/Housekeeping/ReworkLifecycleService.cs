using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private sealed class ReworkStateRow
    {
        public string? State { get; set; }
        public DateTime? DueAt { get; set; }
        public byte[] Revision { get; set; } = [];
    }
    private Task<ReworkStateRow> ReworkState(long id, CancellationToken ct) => db.Database.SqlQuery<ReworkStateRow>(
        $"SELECT workflow_status AS State,due_at AS DueAt,row_version AS Revision FROM dbo.cleaning_reworks WHERE rework_id={id}").SingleAsync(ct);

    // Adapter reads only persisted Inspection/defect source. No Inspection conclusion writer here.
    // Hash excludes mutable rework progress but binds the assignment, due time and complete defect subset.
    public async Task<byte[]> ReworkSourceDigestAsync(long id, CancellationToken ct)
    {
        var json = await db.Database.SqlQuery<string>($"""
            SELECT (SELECT w.rework_id,w.cleaning_task_id,w.inspection_id,w.assignee_user_id,w.requested_at,w.due_at,w.reason,
              i.room_id,i.version_id,i.inspector_user_id,i.result,i.status,i.workflow_status,i.inspected_at,
              CONVERT(varchar(16),i.row_version,2) AS inspection_revision,
              JSON_QUERY((SELECT d.defect_id,d.checklist_item_id,d.description,d.severity_id,d.room_location,r.outcome
                FROM dbo.cleaning_rework_defects l JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id
                LEFT JOIN dbo.inspection_results r ON r.inspection_id=d.inspection_id AND r.checklist_item_id=d.checklist_item_id
                WHERE l.rework_id=w.rework_id ORDER BY d.defect_id FOR JSON PATH,INCLUDE_NULL_VALUES)) AS defects
              FROM dbo.cleaning_reworks w JOIN dbo.room_inspections i ON i.inspection_id=w.inspection_id
              WHERE w.rework_id={id} FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) AS [Value]
            """).SingleAsync(ct);
        return SHA256.HashData(Encoding.UTF8.GetBytes(json));
    }

    public static byte[] ReworkFingerprint(long actor, long branch, long id, string action, string tag, ReworkActionRequest input)
        => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { version = 1, source = "CLEANING_REWORK", actor, branch, id, action, tag,
            note = input.Note.Trim(), defects = input.Remediations?.OrderBy(x => x.DefectId).Select(x => new { x.DefectId, note = x.Note.Trim(),
                photos = x.Photos?.Select(p => new { p.Name, p.MimeType, digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Base64 ?? ""))) }) }) }));

    private async Task<PermissionEvaluation> ReworkAccess(long actor, long branch, long id, string permission, CancellationToken ct)
    {
        var data = new EfPermissionDataSource(db); var now = clock.GetUtcNow().UtcDateTime;
        if (!await data.IsActiveUserAsync(actor, ct) || !await data.IsActiveBranchAsync(branch, ct)) return new(PermissionDecision.Denied);
        // PL-approved candidate binding only; does not grant INSPECTION_VIEW or expand shared resource resolver.
        var own = await db.CleaningReworks.AsNoTracking().AnyAsync(w => w.ReworkId == id && w.AssigneeUserId == actor
            && w.CleaningTask.Room.BranchId == branch && w.Inspection.RoomId == w.CleaningTask.RoomId
            && w.Inspection.CleaningTaskId == w.CleaningTaskId, ct);
        if (!own) return new(PermissionDecision.Denied);
        foreach (var grant in await data.ReadPathsAsync(actor, permission, now, ct))
            if (grant.PermissionCode == permission && PermissionGrantRules.IsEligible(grant, now)
                && (PermissionGrantRules.EffectiveScope(grant) == "GLOBAL" || grant.BranchId == branch
                    && PermissionGrantRules.EffectiveScope(grant) is "BRANCH" or "OWN")) return new(PermissionDecision.Granted);
        return new(PermissionDecision.Denied);
    }

    public async Task<RoomTypeBatchResult> ExecuteReworkAsync(SessionReference ticket, long sid, long branch, long id,
        string action, string? tag, string? key, ReworkActionRequest? input, string? root, CancellationToken ct, long? attachmentId = null)
    {
        if (branch <= 0 || id <= 0 || action is not ("read" or "accept" or "start" or "complete")) throw new ValidationException("Invalid rework command.");
        var read = action == "read";
        if (attachmentId <= 0 || attachmentId != null && !read) throw new ValidationException("Invalid evidence request.");
        if (!read)
        {
            if (!WorkflowStrongTag(tag) || input == null) throw new ValidationException("A strong rework ETag and note are required.");
            CleaningLifecycleRules.Reason(input.Note, 1); CleaningLifecycleRules.Operation(key!);
            if (action == "complete") ReworkLifecycleRules.ValidateRemediations(input.Remediations?.Select(x => x?.DefectId ?? 0).ToArray() ?? [], input.Remediations);
            else if (input.Remediations?.Count > 0) throw new ValidationException("Remediations are accepted only when submitting rework.");
        }
        var operation = read ? Guid.Empty : CleaningLifecycleRules.Operation(key!);
        var fingerprint = read ? [] : ReworkFingerprint(ticket.UserId, branch, id, action, tag!, input!);
        var files = new List<string>(); var commitAttempted = false;
        try { return await Core(false, ct); }
        catch (WorkTimeContractException error) { return LifecycleTimeFailure(error); }
        catch (Exception error) when (!read && error is System.Data.Common.DbException or DbUpdateException or OperationCanceledException)
        {
            if (!commitAttempted) { foreach (var file in files) File.Delete(file); files.Clear(); }
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { return await Core(true, budget.Token); }
            catch (Exception recovery) when (recovery is System.Data.Common.DbException or DbUpdateException or OperationCanceledException or WorkTimeContractException)
            { return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REWORK_OUTCOME_UNCONFIRMED"); }
        }
        finally { if (!commitAttempted) foreach (var file in files) File.Delete(file); }

        async Task<RoomTypeBatchResult> Core(bool recoveryOnly, CancellationToken token)
        {
            ReworkAuthorityProof? trustedProof = null;
            var enabled = candidateNotification != null && await candidateNotification.ReworkAvailableAsync(token);
            var recipients = enabled && action == "complete" ? await candidateNotification!.ClosureRecipientsAsync(branch, token) : [];
            var time = new SqlWorkTimeTransactionParticipant(db); var users = recipients.Append(ticket.UserId).Distinct().Order().ToArray();
            return await BatchAsync(ticket, sid, branch, async (actor, inner) =>
            {
                if (!enabled) return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REWORK_SOURCE_CONTRACT_REQUIRED");
                if (!await HotelOps.Api.Data.HousekeepingSchema.InstalledAsync(db, inner) || !await ResumeTrialInstalled(inner)) return new(RoomTypeBatchStatus.SchemaRequired);
                var row = await db.CleaningReworks.AsNoTracking().Where(w => w.ReworkId == id && w.CleaningTask.Room.BranchId == branch
                    && w.CleaningTask.Room.IsActive && w.CleaningTask.Room.Floor.IsActive && (w.CleaningTask.Room.AreaId == null || w.CleaningTask.Room.Area!.IsActive))
                    .Select(w => new { w.ReworkId, w.CleaningTaskId, w.InspectionId, w.AssigneeUserId, w.RequestedAt, w.CompletedAt,
                        w.CleaningTask.RoomId, w.Inspection.InspectorUserId, w.Inspection.InspectedAt, InspectionRoom = w.Inspection.RoomId,
                        InspectionTask = w.Inspection.CleaningTaskId, w.Inspection.Result, InspectionStatus = w.Inspection.Status }).SingleOrDefaultAsync(inner);
                if (row == null) return new(RoomTypeBatchStatus.NotFound);
                if (row.AssigneeUserId != actor) return new(RoomTypeBatchStatus.Forbidden);
                if (!await DueHousekeeper(actor, branch, DateTime.MaxValue, inner)) return LifecycleFailure(RoomTypeBatchStatus.Forbidden, "REWORK_ACTOR_INELIGIBLE");
                var state = await ReworkState(id, inner); var inspection = await InspectionState(row.InspectionId, inner);
                if (row.InspectionRoom != row.RoomId || row.InspectionTask != row.CleaningTaskId || row.InspectorUserId == actor
                    || row.InspectionStatus != "COMPLETED" || row.Result != "FAIL" || inspection.State != "FAILED"
                    || row.InspectedAt == null || row.RequestedAt < row.InspectedAt || state.DueAt == null
                    || (await CleaningState(row.CleaningTaskId, inner)).State != "REWORK_REQUIRED"
                    || await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId}").SingleOrDefaultAsync(inner) != row.CleaningTaskId
                    || await db.RoomInspections.AnyAsync(i => i.CleaningTaskId == row.CleaningTaskId && i.InspectionId > row.InspectionId, inner))
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive, "REWORK_SOURCE_NOT_CURRENT");
                trustedProof = await ReadTrustedReworkAuthorityAsync(actor,branch,id,root,inner);
                var syntheticProof = trustedProof == null ? await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.fs016_rework_source_proofs WHERE rework_id={id} AND inspection_id={row.InspectionId} AND inspection_revision={inspection.Revision} AND source_digest={await ReworkSourceDigestAsync(id, inner)} AND origin='SYNTHETIC_TEST_ONLY'").SingleAsync(inner) : 0;
                if (trustedProof == null && syntheticProof != 1) return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "INSPECTION_AUTHORITY_UNPROVEN");
                var sourceAuthority = trustedProof?.Origin ?? "SYNTHETIC_TEST_ONLY";
                if (await db.CleaningReworks.CountAsync(w => w.InspectionId == row.InspectionId && w.Status != "COMPLETED" && w.Status != "CANCELLED", inner) != 1)
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive, "DUPLICATE_REWORK_SOURCE");
                var defects = await db.Database.SqlQuery<long>($"SELECT defect_id AS [Value] FROM dbo.cleaning_rework_defects WHERE rework_id={id}").ToArrayAsync(inner);
                if (defects.Length is < 1 or > 200 || await db.InspectionChecklistItems.AnyAsync(i => i.VersionId == db.RoomInspections.Where(x => x.InspectionId == row.InspectionId).Select(x => x.VersionId).First()
                    && i.IsRequired && i.IsActive && !db.InspectionResults.Any(r => r.InspectionId == row.InspectionId && r.ChecklistItemId == i.ItemId && (r.Outcome == "PASS" || r.Outcome == "FAIL")), inner)
                    || await db.InspectionDefects.CountAsync(d => defects.Contains(d.DefectId) && d.InspectionId == row.InspectionId
                    && db.InspectionResults.Any(r => r.InspectionId == d.InspectionId && r.ChecklistItemId == d.ChecklistItemId && r.Outcome == "FAIL"
                        && r.ChecklistItem.VersionId == d.Inspection.VersionId), inner) != defects.Length)
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REWORK_DEFECT_SOURCE_INVALID");
                if (read)
                {
                    var output = new List<ReworkDefectDto>();
                    foreach (var defectId in defects.Order())
                    {
                        var description = await db.InspectionDefects.Where(d => d.DefectId == defectId).Select(d => d.Description).SingleAsync(inner);
                        var remediation = await db.Database.SqlQuery<string?>($"SELECT remediation_note AS [Value] FROM dbo.cleaning_rework_defects WHERE rework_id={id} AND defect_id={defectId}").SingleAsync(inner);
                        var attachments = await db.Database.SqlQuery<long>($"SELECT l.attachment_id AS [Value] FROM dbo.inspection_defect_attachments l JOIN dbo.attachments a ON a.attachment_id=l.attachment_id WHERE l.defect_id={defectId} AND a.deleted_at IS NULL AND a.entity_type='INSPECTION' AND a.entity_id={row.InspectionId}").ToArrayAsync(inner);
                        var ownFiles = await db.Database.SqlQuery<long>($"SELECT e.attachment_id AS [Value] FROM dbo.fs016_rework_evidence e JOIN dbo.attachments a ON a.attachment_id=e.attachment_id WHERE e.rework_id={id} AND e.defect_id={defectId} AND a.deleted_at IS NULL").ToArrayAsync(inner);
                        output.Add(new(defectId, description, remediation, attachments, ownFiles));
                    }
                    if (attachmentId is { } fileId)
                    {
                        var allowed = output.SelectMany(d => d.InspectionAttachmentIds.Concat(d.RemediationAttachmentIds)).ToArray();
                        var metadata = await db.Attachments.AsNoTracking().Where(a => a.AttachmentId == fileId && allowed.Contains(a.AttachmentId) && a.DeletedAt == null)
                            .Select(a => new HotelOps.Api.DTOs.HotelStructure.HistoryEvidenceDto(a.AttachmentId, a.EntityType, a.EntityId,
                                a.OriginalFileName ?? a.FileName, a.MimeType, a.FileSizeBytes, a.UploadedAt, "")).ToArrayAsync(inner);
                        var download = await HistoryFile(metadata, fileId, root, inner);
                        return download == null ? new(RoomTypeBatchStatus.NotFound) : new(RoomTypeBatchStatus.Success, download);
                    }
                    return new(RoomTypeBatchStatus.Success, new ReworkExecutionDto(id, branch, row.CleaningTaskId, row.InspectionId, row.RoomId,
                        actor, state.State!, state.DueAt is { } due ? DateTime.SpecifyKind(due, DateTimeKind.Utc) : null,
                        WorkflowTag(state.Revision), WorkflowTag(inspection.Revision), output, SourceAuthority: sourceAuthority), WorkflowTag(state.Revision));
                }
                var stored = await candidateNotification!.FindAsync(actor, operation, inner);
                if (stored != null)
                {
                    if (!stored.Fingerprint.SequenceEqual(fingerprint)) return new(RoomTypeBatchStatus.Duplicate);
                    var receipt = JsonSerializer.Deserialize<ReworkCommandResult>(stored.ResultJson!, Json);
                    if (!ValidReworkReceipt(receipt, actor, branch, id, row.CleaningTaskId, row.InspectionId, operation, action))
                        return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REWORK_DURABLE_RESULT_INVALID");
                    if (receipt!.SourceAuthority != sourceAuthority) return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "REWORK_AUTHORITY_REPLAY_MISMATCH");
                    await candidateNotification.VerifyReworkAsync(actor, operation, action == "complete", inner);
                    return new(RoomTypeBatchStatus.Success, receipt, receipt!.SourceETag);
                }
                if (recoveryOnly) return new(RoomTypeBatchStatus.Unavailable);
                if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
                var next = ReworkLifecycleRules.Next(state.State, action);
                if (next == null || row.CompletedAt != null) return new(RoomTypeBatchStatus.Inactive);
                var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(inner), DateTimeKind.Utc);
                long? interval = null;
                if (action == "complete")
                {
                    ReworkLifecycleRules.ValidateRemediations(defects, input!.Remediations);
                    var unresolved = await db.Database.SqlQuery<int>($"""
                        SELECT COUNT(*) AS [Value] FROM dbo.cleaning_progress_logs p LEFT JOIN dbo.fs014_cleaning_blocker_resolutions b ON b.progress_id=p.progress_id
                        WHERE p.cleaning_task_id={row.CleaningTaskId} AND p.status='BLOCKED' AND (b.progress_id IS NULL OR b.resolved_at IS NULL OR b.incident_id IS NOT NULL OR b.material_request_id IS NOT NULL)
                        """).SingleAsync(inner);
                    if (unresolved != 0) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "ACTIVE_OR_UNCONFIRMED_BLOCKER");
                    if (recipients.Length == 0 || !(await candidateNotification.ClosureRecipientsAsync(branch, inner)).SequenceEqual(recipients))
                        return LifecycleFailure(RoomTypeBatchStatus.Unavailable, "AUTHORIZED_SUPERVISOR_REQUIRED");
                    var open = await db.WorkIntervals.AsNoTracking().Where(w => w.WorkItemKind == "REWORK" && w.WorkItemId == id && w.ClosedAt == null).Take(2).ToArrayAsync(inner);
                    if (open.Length != 1 || open[0].UserId != actor || open[0].BranchId != branch) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_WORK_INTERVAL_REQUIRED");
                    var closed = await time.CloseIntervalAsync(new(actor, actor, branch, "REWORK", id), open[0].IntervalId, open[0].RowVersion, inner);
                    interval = closed.IntervalId; now = closed.ClosedAtUtc!.Value; db.ChangeTracker.Clear();
                    foreach (var remediation in input.Remediations!)
                    {
                        var note = CleaningLifecycleRules.Reason(remediation.Note, 1);
                        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_rework_defects SET remediation_note={note},remediated_by={actor},remediated_at={now} WHERE rework_id={id} AND defect_id={remediation.DefectId}", inner);
                        foreach (var photo in remediation.Photos ?? [])
                        {
                            if (root == null) throw new ValidationException("Private evidence storage required.");
                            var attachment = await StoreFeatureEvidence("CLEANING_TASK", row.CleaningTaskId, actor, photo, root, files, inner);
                            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.fs016_rework_evidence(rework_id,defect_id,attachment_id) VALUES({id},{remediation.DefectId},{attachment})", inner);
                        }
                    }
                }
                var legacyStatus = next == "ACCEPTED" ? "PENDING" : "IN_PROGRESS";
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET workflow_status={next},status={legacyStatus},note={input!.Note.Trim()} WHERE rework_id={id}", inner);
                if (action == "start") { interval = (await time.OpenIntervalAsync(new(actor, actor, branch, "REWORK", id), inner)).IntervalId; db.ChangeTracker.Clear(); }
                var readiness = action == "complete" ? "PENDING_INSPECTION" : "REWORK_REQUIRED";
                var blocked = await db.RoomBlocks.AsNoTracking().AnyAsync(b => b.RoomId == row.RoomId && b.ReleasedAt == null, inner);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at) VALUES({row.RoomId},{row.CleaningTaskId},{readiness},{blocked},{actor},{now})", inner);
                var result = new ReworkCommandResult(id, branch, row.CleaningTaskId, row.InspectionId, actor, action, next, operation,
                    WorkflowTag((await ReworkState(id, inner)).Revision), now, interval, action == "complete" ? Guid.NewGuid() : null,
                    readiness, action == "complete" ? "PENDING_DELIVERY" : "NOT_REQUIRED", SourceAuthority: sourceAuthority);
                await BatchAuditAsync(actor, branch, "cleaning_reworks", id, new { state.State }, new { operationId = operation,
                    action, note = input.Note.Trim(), sourceAuthority, entity = result }, ["workflowStatus", "remediation", "workInterval", "readiness", "notification"], inner);
                await candidateNotification.RememberReworkAsync(actor, fingerprint, row.RoomId, recipients, result, inner);
                return new(RoomTypeBatchStatus.Success, result, result.SourceETag);
            }, token, requireRoomTypeSchema: false, additionalUsers: users, preserveCommitErrors: true,
                afterUserLocks: !read && enabled && !recoveryOnly ? inner => time.LockUsersAsync(users, inner) : null,
                featurePermission: inner => ReworkAccess(ticket.UserId, branch, id, read ? "CLEANING_VIEW" : "CLEANING_EXECUTE", inner),
                beforeCommit: async inner =>
                {
                    if (!await candidateNotification!.ReworkAvailableAsync(inner)) throw new InvalidOperationException("REWORK_CANDIDATE_DISABLED");
                    if (trustedProof != null)
                    {
                        var revalidated = await ReadTrustedReworkAuthorityAsync(ticket.UserId,branch,id,root,inner);
                        if (revalidated == null || revalidated.OperationId != trustedProof.OperationId
                            || revalidated.Issuer != trustedProof.Issuer || !revalidated.SourceDigest.SequenceEqual(trustedProof.SourceDigest))
                            throw new InvalidOperationException("REWORK_AUTHORITY_CHANGED_BEFORE_COMMIT");
                    }
                    if (!read && action == "complete" && !(await candidateNotification.ClosureRecipientsAsync(branch, inner)).SequenceEqual(recipients))
                        throw new InvalidOperationException("SUPERVISOR_SCOPE_CHANGED");
                    commitAttempted = true;
                });
        }
    }

    public static bool ValidReworkReceipt(ReworkCommandResult? r, long actor, long branch, long id, long task, long inspection, Guid op, string action)
        => r != null && r.Id == id && r.BranchId == branch && r.AssigneeId == actor && r.CleaningTaskId == task
        && r.InspectionId == inspection && r.OperationId == op && r.Action == action && WorkflowStrongTag(r.SourceETag)
        && r.Contract == "PROVISIONAL_NOT_ACCEPTED" && r.OccurredAtUtc.Kind == DateTimeKind.Utc
        && r.SourceAuthority is "SYNTHETIC_TEST_ONLY" or ReworkAuthorityContract.Origin
        && r.State == (action == "accept" ? "ACCEPTED" : action == "start" ? "IN_PROGRESS" : "PENDING_REINSPECTION")
        && r.Readiness == (action == "complete" ? "PENDING_INSPECTION" : "REWORK_REQUIRED")
        && (action == "accept" ? r.IntervalId == null : r.IntervalId > 0)
        && (action == "complete" ? r.PublicationId is { } p && p != Guid.Empty && r.Notification == "PENDING_DELIVERY"
            : r.PublicationId == null && r.Notification == "NOT_REQUIRED");
}
