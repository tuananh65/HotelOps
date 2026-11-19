using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public async Task<RoomTypeBatchResult> ConcludeReinspectionAsync(SessionReference ticket, long sid, long branch,
        long inspection, string? tag, string? key, ConcludeReinspectionRequest input, string? root, CancellationToken ct)
    {
        if (branch <= 0 || inspection <= 0 || input == null || !WorkflowStrongTag(tag)
            || !WorkflowStrongTag(input.ReworkETag) || !WorkflowStrongTag(input.BindingETag)
            || input.Defects == null || input.Defects.Count is < 1 or > 200
            || input.Defects.Any(d => d == null || d.DefectId <= 0 || d.Fixed == null)
            || input.Defects.Select(d => d.DefectId).Distinct().Count() != input.Defects.Count
            || input.Defects.Sum(d => d.Photos?.Count ?? 0) > 5)
            throw new ValidationException("Exact defect decisions and strong inspection/rework/binding ETags are required.");
        var reason = WorkflowReason(input.Reason);
        var decisions = input.Defects.OrderBy(d => d.DefectId)
            .Select(d => d with { Note = ChecklistText(d.Note, 500, false) }).ToArray();
        var operation = CleaningLifecycleRules.Operation(key!);
        var fingerprint = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { contract = ReinspectionConclusionContract, actor = ticket.UserId, branch, inspection, tag, input.ReworkETag, input.BindingETag, reason, decisions }, Json));
        using var evidence = new EvidenceFileLifetime();
        return await BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await ReinspectionWorkflowInstalled(token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var prior = await ReadReinspectionOperation<ReinspectionConclusionResult>(actor, branch, inspection,
                operation, fingerprint, ReinspectionConclusionContract, token);
            if (prior.Exists) return prior.Value is { } acknowledged
                ? new(RoomTypeBatchStatus.Success, acknowledged, acknowledged.SourceETag)
                : LifecycleFailure(RoomTypeBatchStatus.Duplicate, "REINSPECTION_OPERATION_CONFLICT");
            if (await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WITH(HOLDLOCK)
                WHERE actor_user_id={actor} AND operation_key={operation}
                """).SingleAsync(token) != 0) return new(RoomTypeBatchStatus.Duplicate);
            if (!await CheckReinspectionWriter("FE05.UC047", "dbo.fe05_commit_reinspection", true, token))
                return new(RoomTypeBatchStatus.Unavailable);
            var loaded = await ReadReinspectionInCallerTransactionAsync(actor, branch, inspection, root, null, token);
            if (loaded.Status != RoomTypeBatchStatus.Success || loaded.Value is not ReinspectionExecutionDto source) return loaded;
            if (source.SourceETag != tag || source.ReworkETag != input.ReworkETag || source.BindingETag != input.BindingETag)
                return new(RoomTypeBatchStatus.Stale);
            var validation = ReinspectionDecisionValidator.Validate(source.Defects.Select(d => d.Id).ToArray(),
                decisions.Select(d => new ReinspectionDefectDecision(d.DefectId, d.Fixed)).ToArray(),
                Convert.FromHexString(source.ReworkETag.Trim('"')), input.ReworkETag,
                Convert.FromHexString(source.SourceETag.Trim('"')), tag);
            if (!validation.IsComplete) return new(RoomTypeBatchStatus.InvalidReference);
            // This endpoint has no draft-save authority: preexisting decisions must not be silently overwritten.
            if (source.Defects.Any(d => d.Fixed != null || d.Note != null || d.ReinspectionPhotos.Count != 0))
                return new(RoomTypeBatchStatus.Unavailable);
            var room = await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={source.RoomId}")
                .AsNoTracking().SingleAsync(token);
            var now = clock.GetUtcNow().UtcDateTime;
            var readiness = new EffectiveRoomReadiness(db);
            var before = (await readiness.ReadAsync([source.RoomId], now, token))[source.RoomId]; // Hold effective-block ranges through commit.
            var encoded = JsonSerializer.Serialize(decisions.Select(d => new { defectId = d.DefectId, fixedResult = d.Fixed, note = d.Note }), Json);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE r SET fixed=j.fixed_result,note=j.note,recorded_by={actor},recorded_at={now}
                FROM dbo.fe05_reinspection_defect_results_v1 r
                JOIN OPENJSON({encoded}) WITH(defect_id bigint '$.defectId',fixed_result bit '$.fixedResult',note nvarchar(500) '$.note') j ON j.defect_id=r.defect_id
                WHERE r.inspection_id={inspection} AND r.rework_id={source.ReworkId} AND r.fixed IS NULL;
                IF @@ROWCOUNT<>{decisions.Length} THROW 51999,'Reinspection exact result write conflict',1;
                """, token);
            var originals = await db.InspectionDefects.AsNoTracking().Where(d => decisions.Select(x => x.DefectId).Contains(d.DefectId)).ToArrayAsync(token);
            var newDefects = new Dictionary<long, long>();
            foreach (var group in originals.GroupBy(d => d.ChecklistItemId!.Value))
            {
                var fixedGroup = group.All(d => decisions.Single(x => x.DefectId == d.DefectId).Fixed == true);
                db.Add(new InspectionResult { InspectionId = inspection, ChecklistItemId = group.Key,
                    Outcome = fixedGroup ? "PASS" : "FAIL", Note = reason, InspectedAt = now });
            }
            await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            foreach (var decision in decisions)
            {
                if (decision.Fixed == false)
                {
                    var original = originals.Single(d => d.DefectId == decision.DefectId);
                    var defect = new InspectionDefect { InspectionId = inspection, ChecklistItemId = original.ChecklistItemId,
                        SeverityId = original.SeverityId, Description = original.Description, CorrectiveAction = original.CorrectiveAction, Status = "OPEN" };
                    db.Add(defect); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
                    newDefects.Add(decision.DefectId, defect.DefectId);
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE target SET room_location=original.room_location FROM dbo.inspection_defects target
                        CROSS JOIN dbo.inspection_defects original WHERE target.defect_id={defect.DefectId} AND original.defect_id={decision.DefectId};
                        """, token);
                }
                foreach (var photo in decision.Photos ?? [])
                {
                    if (root == null) throw new ValidationException("Private evidence storage required.");
                    var file = await StoreFeatureEvidence("INSPECTION", inspection, actor, photo, root, evidence.Files, token);
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT dbo.fe05_reinspection_defect_attachments_v1(inspection_id,defect_id,attachment_id)
                        VALUES({inspection},{decision.DefectId},{file});
                        """, token);
                    if (newDefects.TryGetValue(decision.DefectId, out var newDefect))
                        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.inspection_defect_attachments(defect_id,attachment_id) VALUES({newDefect},{file})", token);
                }
            }
            var passed = validation.AllFixed == true;
            var inspectionState = passed ? "PASSED" : "FAILED";
            var reworkState = passed ? "COMPLETED" : "FAILED";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.room_inspections SET workflow_status={inspectionState},status='COMPLETED',result={(passed ? "PASS" : "FAIL")},
                  inspected_at={now},summary_note={reason}
                WHERE inspection_id={inspection} AND inspector_user_id={actor} AND workflow_status='ASSIGNED' AND status='PENDING';
                IF @@ROWCOUNT<>1 THROW 51999,'Reinspection phase conflict',1;
                UPDATE dbo.cleaning_reworks SET workflow_status={reworkState},status='COMPLETED',completed_at={now}
                WHERE rework_id={source.ReworkId} AND workflow_status='PENDING_REINSPECTION' AND row_version={Convert.FromHexString(input.ReworkETag.Trim('"'))};
                IF @@ROWCOUNT<>1 THROW 51999,'Rework conclusion conflict',1;
                UPDATE dbo.cleaning_tasks SET workflow_status={(passed ? "COMPLETED" : "REWORK_REQUIRED")},status={(passed ? "COMPLETED" : "IN_PROGRESS")}
                WHERE cleaning_task_id={source.CleaningTaskId} AND room_id={source.RoomId} AND workflow_status='REWORK_REQUIRED' AND status='IN_PROGRESS';
                IF @@ROWCOUNT<>1 THROW 51999,'Cleaning conclusion conflict',1;
                """, token);
            var after = (await readiness.ReadAsync([source.RoomId], now, token))[source.RoomId];
            if (passed ? after.Readiness is not ("READY" or "BLOCKED") : after.Readiness is not ("REWORK_REQUIRED" or "BLOCKED"))
                throw new InvalidOperationException("REINSPECTION_READINESS_CONFLICT");
            var physicalStatus = EffectiveRoomReadiness.InspectionOperationalStatus(room.OperationalStatus, after.CanUse);
            if (physicalStatus != room.OperationalStatus)
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE dbo.rooms SET operational_status='READY' WHERE room_id={source.RoomId} AND branch_id={branch} AND operational_status={room.OperationalStatus} AND is_active=1;
                    IF @@ROWCOUNT<>1 THROW 51999,'Reinspection room status conflict',1;
                    """, token);
            await RoomReadinessHistory.RecordAsync(db, before, after, room.OperationalStatus,
                physicalStatus, "INSPECTION", inspection, actor, now, reason, token);
            var result = new ReinspectionConclusionResult(inspection, source.ReworkId, branch, source.CleaningTaskId, source.RoomId,
                inspectionState, reworkState, WorkflowTag((await InspectionState(inspection, token)).Revision),
                WorkflowTag((await ReworkState(source.ReworkId, token)).Revision), operation, after.Readiness,
                after.Readiness == "BLOCKED" ? "MSG17" : "MSG08");
            var entity = JsonSerializer.Serialize(result, Json);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC dbo.fe05_commit_reinspection @actor={actor},@branch={branch},@inspection={inspection},@operation={operation},@fingerprint={fingerprint},@entity={entity};
                """, token);
            var written = await ReadWrittenReinspectionResult<ReinspectionConclusionResult>(actor, operation, token);
            if (written != result) throw new InvalidOperationException("REINSPECTION_CONCLUSION_RECEIPT_INVALID");
            return new(RoomTypeBatchStatus.Success, written, written.SourceETag);
        }, ct, requireRoomTypeSchema: false, permissionCode: "INSPECTION_PERFORM", preserveCommitErrors: true,
            eligibility: token => EligibleInspector(ticket.UserId, branch, token),
            beforeCommit: evidence.BeforeCommitAsync);
    }
}
