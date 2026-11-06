using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public async Task<RoomTypeBatchResult> AssignCleaningCandidateAsync(SessionReference ticket, long sid, long branch, long task, string tag, string operationKey, AssignCleaningRequest input, CancellationToken ct)
    {
        try
        {
            return await AssignCleaningCandidateCoreAsync(ticket, sid, branch, task, tag, operationKey, input, false, ct);
        }
        catch (Exception error) when (error is System.Data.Common.DbException or DbUpdateException or OperationCanceledException)
        {
            // BatchAsync has disposed its transaction before recovery. A lost commit acknowledgement
            // is never success by itself. Reauthorize and look up the original immutable command only.
            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                return await AssignCleaningCandidateCoreAsync(ticket, sid, branch, task, tag, operationKey, input, true, recovery.Token);
            }
            catch (Exception recoveryError) when (recoveryError is System.Data.Common.DbException or DbUpdateException or OperationCanceledException)
            {
                return new(RoomTypeBatchStatus.Unavailable);
            }
        }
    }

    private async Task<RoomTypeBatchResult> AssignCleaningCandidateCoreAsync(SessionReference ticket, long sid, long branch, long task, string tag, string operationKey, AssignCleaningRequest input, bool recoveryOnly, CancellationToken ct)
    {
        if (candidateNotification == null || !await candidateNotification.AvailableAsync(ct)) return await AssignCleaningAsync(ticket, sid, branch, task, tag, input, ct);
        if (task <= 0 || input.AssigneeId <= 0 || !WorkflowStrongTag(tag) || !Guid.TryParseExact(operationKey, "D", out var operation) || operation == Guid.Empty || input.AdoptUnassignedLegacy)
            throw new ValidationException("Strong task ETag, GUID operation and nonlegacy command required.");
        var reason = WorkflowReason(input.Reason);
        if (input.DueAt.Kind != DateTimeKind.Utc) throw new ValidationException("UTC due required.");
        var fingerprint = Convert.FromHexString(CleaningAssignmentCommandIdentity.Fingerprint(ticket.UserId, branch, task, tag, input));
        System.Data.Common.DbTransaction? physical = null; bool created = false;
        return await BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            physical = db.Database.CurrentTransaction!.GetDbTransaction();
            // Current branch/source authorization always precedes durable replay. Eligibility only for a new mutation.
            var row = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(x => x.CleaningTaskId == task && x.Room.BranchId == branch && x.Room.IsActive, token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var prior = await candidateNotification.FindAsync(actor, operation, token);
            if (prior != null)
            {
                if (!prior.Fingerprint.SequenceEqual(fingerprint)) return new(RoomTypeBatchStatus.Duplicate);
                if (!prior.Completed || prior.ResultJson == null) return new(RoomTypeBatchStatus.Unavailable);
                var stored = ProvisionalCleaningNotification.ReadResult(prior.ResultJson);
                if (stored.Id != task || stored.BranchId != branch) return new(RoomTypeBatchStatus.Unavailable);
                CandidateCommitGuard.Instance.Arm(physical, actor, operation);
                return new(RoomTypeBatchStatus.Success, stored, stored.SourceETag);
            }
            if (recoveryOnly) return new(RoomTypeBatchStatus.Unavailable);
            CandidateCommitGuard.Instance.Arm(physical, actor, operation, mutation: true);
            WorkflowDue(input.DueAt, clock.GetUtcNow().UtcDateTime);
            if (!await DueHousekeeper(input.AssigneeId, branch, input.DueAt, token)) return new(RoomTypeBatchStatus.InvalidReference);
            var state = await CleaningState(task, token);
            if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
            if (state.State != "PENDING_ASSIGNMENT") return new(RoomTypeBatchStatus.Inactive);
            if (await db.CleaningAssignments.AnyAsync(x => x.CleaningTaskId == task && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null, token)) return new(RoomTypeBatchStatus.Duplicate);
            var cycle = await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS [Value] FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId}").SingleOrDefaultAsync(token);
            if (cycle != task) return new(RoomTypeBatchStatus.Inactive);
            var assigned = new CleaningAssignment { CleaningTaskId = task, AssigneeUserId = input.AssigneeId, AssignedBy = actor, AssignedAt = clock.GetUtcNow().UtcDateTime, AssignmentStatus = "ACTIVE", Reason = reason };
            db.CleaningAssignments.Add(assigned); await db.SaveChangesAsync(token); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status='ASSIGNED',status='PENDING',due_at={input.DueAt} WHERE cleaning_task_id={task}", token);
            var revision = WorkflowTag((await CleaningState(task, token)).Revision);
            var publication = Guid.NewGuid();
            var result = new CandidateAssignmentResult(task, assigned.AssignmentId, branch, assigned.AssigneeUserId, operation, publication, "ASSIGNED", "PENDING_DELIVERY", revision);
            await BatchAuditAsync(actor, branch, "cleaning_tasks", task, new { state.State }, new { assignmentId = assigned.AssignmentId, operationId = operation, entity = result }, ["assignee", "workflowStatus", "dueAt"], token);
            created = true;
            await candidateNotification.StageAsync(new(operation, publication, actor, branch, task, cycle, row.RoomId, assigned.AssignmentId, assigned.AssigneeUserId, assigned.AssignedAt, revision, input.DueAt), fingerprint, result, token);
            return new(RoomTypeBatchStatus.Success, result, revision);
        }, ct, additionalUsers: [input.AssigneeId], permissionCode: "CLEANING_ASSIGN", preserveCommitErrors: true, beforeCommit: async token =>
        {
            if (physical == null) throw new InvalidOperationException("TRANSACTION_NOT_CAPTURED");
            if (created && !await DueHousekeeper(input.AssigneeId, branch, input.DueAt, token)) throw new ValidationException("Assignee eligibility changed before commit.");
            await candidateNotification.VerifyBeforeCommitAsync(physical, ticket.UserId, operation, token);
            if (created) await CandidateCommitGuard.VerifyAsync(physical, ticket.UserId, operation, token, mutation: true);
        });
    }
}
