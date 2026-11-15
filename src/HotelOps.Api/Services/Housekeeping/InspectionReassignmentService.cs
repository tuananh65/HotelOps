using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<bool> InspectionReassignmentAvailable(CancellationToken ct) =>
        await InitialInspectionWriterAvailable(false, ct)
        && await CheckReinspectionWriter("FE05.UC041.REASSIGN", "dbo.fe05_reassign_inspection", true, ct);

    public Task<RoomTypeBatchResult> ReadInspectionReassignmentAsync(SessionReference ticket, long sid, long branch, long id, CancellationToken ct)
    {
        if (id <= 0 || branch <= 0) throw new ValidationException("Inspection scope required.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var row = await db.RoomInspections.AsNoTracking().Where(i => i.InspectionId == id && i.Room.BranchId == branch
                && i.Room.IsActive && i.Room.Floor.IsActive && (i.Room.AreaId == null || i.Room.Area!.IsActive))
                .Select(i => new { i.RoomId, i.CleaningTaskId, i.VersionId, i.InspectorUserId, i.Status }).SingleOrDefaultAsync(token);
            if (row?.CleaningTaskId is not { } task) return new(RoomTypeBatchStatus.NotFound);
            var state = await InspectionState(id, token);
            if (state.State is not ("ASSIGNED" or "INSPECTING" or "PENDING_CONCLUSION") || row.Status is "COMPLETED" or "CANCELLED"
                || !await ReworkCurrentCycle(row.RoomId, task, token) || (await CleaningState(task, token)).State != "PENDING_INSPECTION"
                || await db.CleaningReworks.AnyAsync(w => w.CleaningTaskId == task, token)) return new(RoomTypeBatchStatus.Inactive);
            var cycle = await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={row.RoomId} AND cleaning_task_id={task}").SingleAsync(token);
            var enabled = await InspectionReassignmentAvailable(token)
                && (await permissions.EvaluateBranchAsync("INSPECTION_PERFORM", branch, token)).IsGranted;
            var choices = enabled ? await EligibleInspectors(branch, clock.GetUtcNow().UtcDateTime)
                .Where(u => u.UserId != row.InspectorUserId
                    && !db.CleaningAssignments.Any(a => a.CleaningTaskId == task && a.AssigneeUserId == u.UserId && a.AssignmentStatus != "CANCELLED")
                    && !db.CleaningProgressLogs.Any(p => p.CleaningTaskId == task && p.LoggedBy == u.UserId))
                .OrderBy(u => u.FullName).ThenBy(u => u.UserId).Select(u => new ReinspectionAssigneeDto(u.UserId, u.FullName)).ToArrayAsync(token) : [];
            return new(RoomTypeBatchStatus.Success, new InspectionReassignmentOptions(id, branch, task, row.RoomId, row.VersionId,
                row.InspectorUserId, state.State!, state.DueAt, WorkflowTag(state.Revision), WorkflowTag(cycle), choices, enabled && choices.Length > 0), WorkflowTag(state.Revision));
        }, ct, permissionCode: "INSPECTION_VIEW", eligibility: token => EligibleInspector(ticket.UserId, branch, token));
    }

    public Task<RoomTypeBatchResult> ReassignInspectionAsync(SessionReference ticket, long sid, long branch, long id, string? tag,
        string? key, InspectionReassignmentRequest input, CancellationToken ct)
    {
        if (id <= 0 || branch <= 0 || input == null || input.InspectorId <= 0 || !WorkflowStrongTag(tag) || !WorkflowStrongTag(input.CycleETag))
            throw new ValidationException("Inspection, inspector and exact source/cycle revisions required.");
        var reason = WorkflowReason(input.Reason); var operation = CleaningLifecycleRules.Operation(key!);
        var fingerprint = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { contract = InspectionReassignmentSource.Contract,
            actor = ticket.UserId, branch, id, tag, input.InspectorId, input.CycleETag, reason }, Json));
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token) || !await InspectionReassignmentAvailable(token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var prior = await db.Database.SqlQuery<SubmissionLedgerRow>($"SELECT result_json AS Json,fingerprint AS Fingerprint,operation_key AS OperationId FROM dbo.notification_operation_reservations WITH(HOLDLOCK) WHERE actor_user_id={actor} AND operation_key={operation} AND completed=1 AND fingerprint_version=1").SingleOrDefaultAsync(token);
            if (prior != null)
            {
                var audits = await db.AuditLogs.AsNoTracking().Where(a => a.ActorUserId == actor && a.BranchId == branch && a.EntityType == "INSPECTION_ASSIGNMENT"
                    && a.Action == "UPDATE" && a.NewValues == prior.Json).Take(2).ToArrayAsync(token);
                var proof = audits.Length == 1 ? await InspectionReassignmentSource.VerifyAsync(db, audits[0], token) : null;
                if (proof == null || proof.InspectionId != id || proof.OperationId != operation || !CryptographicOperations.FixedTimeEquals(prior.Fingerprint, fingerprint))
                    return new(RoomTypeBatchStatus.Duplicate);
                return new(RoomTypeBatchStatus.Success, proof.Entity, proof.Entity.SourceETag);
            }
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WITH(HOLDLOCK) WHERE actor_user_id={actor} AND operation_key={operation}").SingleAsync(token) != 0) return new(RoomTypeBatchStatus.Duplicate);
            var row = await db.RoomInspections.AsNoTracking().Where(i => i.InspectionId == id && i.Room.BranchId == branch
                && i.Room.IsActive && i.Room.Floor.IsActive && (i.Room.AreaId == null || i.Room.Area!.IsActive))
                .Select(i => new { i.RoomId, i.CleaningTaskId, i.VersionId, i.InspectorUserId, i.Status }).SingleOrDefaultAsync(token);
            if (row?.CleaningTaskId is not { } task) return new(RoomTypeBatchStatus.NotFound);
            var state = await InspectionState(id, token);
            if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
            if (state.State is not ("ASSIGNED" or "INSPECTING" or "PENDING_CONCLUSION") || row.Status is "COMPLETED" or "CANCELLED"
                || !await ReworkCurrentCycle(row.RoomId, task, token) || (await CleaningState(task, token)).State != "PENDING_INSPECTION"
                || await db.CleaningReworks.AnyAsync(w => w.CleaningTaskId == task, token)) return new(RoomTypeBatchStatus.Inactive);
            var cycle = await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={row.RoomId} AND cleaning_task_id={task}").SingleAsync(token);
            if (input.CycleETag != WorkflowTag(cycle)) return new(RoomTypeBatchStatus.Stale);
            if (input.InspectorId == row.InspectorUserId) return new(RoomTypeBatchStatus.Duplicate);
            if (await InspectorWorkedOnCycle(input.InspectorId, task, token)) return new(RoomTypeBatchStatus.InvalidReference);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fe05_reassign_inspection @actor={actor},@branch={branch},@inspection={id},@inspector={input.InspectorId},@revision={state.Revision},@cycleRevision={cycle},@reason={reason},@operation={operation},@fingerprint={fingerprint}", token);
            var result = await ReadWrittenReinspectionResult<InspectionReassignmentResult>(actor, operation, token);
            if (!InspectionReassignmentSource.ValidResult(result) || result.Id != id || result.TaskId != task || result.BranchId != branch
                || result.RoomId != row.RoomId || result.VersionId != row.VersionId || result.PreviousInspectorId != row.InspectorUserId || result.InspectorId != input.InspectorId
                || result.State != state.State || result.OperationId != operation) throw new InvalidOperationException("REASSIGNMENT_RECEIPT_INVALID");
            return new(RoomTypeBatchStatus.Success, result, result.SourceETag);
        }, ct, additionalUsers: [input.InspectorId], permissionCode: "INSPECTION_PERFORM", preserveCommitErrors: true,
            eligibility: async token => await EligibleInspector(ticket.UserId, branch, token) && await EligibleInspector(input.InspectorId, branch, token));
    }
}
