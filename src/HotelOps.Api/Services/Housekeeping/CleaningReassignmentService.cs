using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public async Task<RoomTypeBatchResult> ReassignCleaningAsync(SessionReference ticket, long sid, long branch, long task, string tag, string key, ReassignCleaningRequest request, CancellationToken ct)
    {
        if (task <= 0 || request.AssigneeId <= 0 || !CleaningLifecycleRules.StrongTag(tag)) throw new ValidationException("Positive task/assignee and strong ETag required.");
        var operation = CleaningLifecycleRules.Operation(key);
        var normalized = request with { Reason = CleaningLifecycleRules.Reason(request.Reason) };
        if (request.DueAt.Kind != DateTimeKind.Utc) throw new ValidationException("UTC deadline required.");
        try { return await ReassignCleaningCoreAsync(ticket, sid, branch, task, tag, operation, normalized, false, ct); }
        catch (WorkTimeContractException error) { return LifecycleTimeFailure(error); }
        catch (Exception error) when (error is System.Data.Common.DbException or DbUpdateException or OperationCanceledException)
        {
            // The caller transaction is disposed before lookup; recovery never performs a new mutation.
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { return await ReassignCleaningCoreAsync(ticket, sid, branch, task, tag, operation, normalized, true, budget.Token); }
            catch (Exception retryError) when (retryError is System.Data.Common.DbException or DbUpdateException or OperationCanceledException or WorkTimeContractException)
            { return new(RoomTypeBatchStatus.Unavailable); }
        }
    }
    private async Task<RoomTypeBatchResult> ReassignCleaningCoreAsync(SessionReference ticket, long sid, long branch, long task, string tag, Guid operation, ReassignCleaningRequest request, bool recoveryOnly, CancellationToken ct)
    {
        // Owner hint is never authority. Re-read under the full actor/old/new user lock set below.
        var hint = await db.CleaningAssignments.AsNoTracking().Where(x => x.CleaningTaskId == task && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null
            && x.CleaningTask.Room.BranchId == branch).Select(x => (long?)x.AssigneeUserId).FirstOrDefaultAsync(ct);
        var users = new[] { ticket.UserId, request.AssigneeId, hint ?? ticket.UserId }.Distinct().Order().ToArray();
        var time = new SqlWorkTimeTransactionParticipant(db);
        var fingerprint = CleaningLifecycleRules.Fingerprint(ticket.UserId, branch, task, tag, request);
        var mutation = false;
        return await BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (candidateNotification == null || !await candidateNotification.AvailableAsync(token) || !await CleaningExecutionInstalled(token)) return new(RoomTypeBatchStatus.Unavailable);
            var row = await db.CleaningTasks.AsNoTracking().SingleOrDefaultAsync(x => x.CleaningTaskId == task && x.Room.BranchId == branch
                && x.Room.IsActive && x.Room.Floor.IsActive && (x.Room.AreaId == null || x.Room.Area!.IsActive), token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var previous = await db.Database.SqlQuery<ReassignStoredCommand>($"SELECT fingerprint AS Fingerprint,result_json AS ResultJson FROM dbo.fs013_reassignment_operations WITH(UPDLOCK,HOLDLOCK) WHERE actor_user_id={actor} AND operation_key={operation}").ToArrayAsync(token);
            if (previous.Length == 1)
            {
                if (!previous[0].Fingerprint.SequenceEqual(fingerprint)) return new(RoomTypeBatchStatus.Duplicate);
                var stored = JsonSerializer.Deserialize<ReassignedCleaningDto>(previous[0].ResultJson, Json) ?? throw new InvalidOperationException("INVALID_DURABLE_REASSIGN_RESULT");
                if (stored.Id != task || stored.BranchId != branch) return new(RoomTypeBatchStatus.Unavailable);
                return new(RoomTypeBatchStatus.Success, stored, stored.SourceETag);
            }
            if (recoveryOnly) return new(RoomTypeBatchStatus.Unavailable);
            WorkflowDue(request.DueAt, clock.GetUtcNow().UtcDateTime);
            var state = await CleaningState(task, token);
            if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
            var assignments = await db.CleaningAssignments.AsNoTracking().Where(x => x.CleaningTaskId == task && x.AssignmentStatus == "ACTIVE" && x.EndedAt == null).Take(2).ToArrayAsync(token);
            if (assignments.Length != 1) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "CURRENT_ASSIGNMENT_REQUIRED");
            var old = assignments[0];
            if (hint != old.AssigneeUserId) return new(RoomTypeBatchStatus.Stale);
            var conflict = CleaningLifecycleRules.ReassignConflict(state.State, old.AssigneeUserId, request.AssigneeId);
            if (conflict != null) return LifecycleFailure(RoomTypeBatchStatus.InvalidReference, conflict);
            if (!await DueHousekeeper(request.AssigneeId, branch, request.DueAt, token)) return new(RoomTypeBatchStatus.InvalidReference);
            var interval = await db.WorkIntervals.AsNoTracking().Where(x => x.WorkItemKind == "CLEANING" && x.WorkItemId == task && x.ClosedAt == null).Take(2).ToArrayAsync(token);
            long? closedIntervalId = null;
            if (state.State == "IN_PROGRESS")
            {
                if (interval.Length != 1 || interval[0].UserId != old.AssigneeUserId) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "SOURCE_INTERVAL_REQUIRED");
                await time.CloseIntervalAsync(new(actor, old.AssigneeUserId, branch, "CLEANING", task), interval[0].IntervalId, interval[0].RowVersion, token);
                closedIntervalId = interval[0].IntervalId;
            }
            else if (interval.Length != 0) return LifecycleFailure(RoomTypeBatchStatus.Inactive, "SOURCE_INTERVAL_STATE_CONFLICT");
            var expected = Convert.FromHexString(tag.Trim('"'));
            var values = await db.Database.SqlQuery<string>($"EXEC dbo.fs013_reassign_with_evidence @actor={actor},@branch={branch},@task={task},@previous={old.AssignmentId},@assignee={request.AssigneeId},@operation={operation},@fingerprint={fingerprint},@expected={expected},@due={request.DueAt},@reason={request.Reason},@closedInterval={closedIntervalId}").ToArrayAsync(token);
            if (values.Length != 1) throw new InvalidOperationException("REASSIGN_RESULT_REQUIRED");
            var result = JsonSerializer.Deserialize<ReassignedCleaningDto>(values[0], Json) ?? throw new InvalidOperationException("REASSIGN_RESULT_REQUIRED");
            mutation = true;
            return new(RoomTypeBatchStatus.Success, result, result.SourceETag);
        }, ct, additionalUsers: users, permissionCode: "CLEANING_ASSIGN", requireRoomTypeSchema: false, preserveCommitErrors: true,
            afterUserLocks: token => time.LockUsersAsync(users, token), beforeCommit: async token =>
            {
                if (mutation && !await DueHousekeeper(request.AssigneeId, branch, request.DueAt, token)) throw new ValidationException("New assignee eligibility changed.");
                await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fs013_verify_reassignment @actor={ticket.UserId},@operation={operation},@mutation={mutation}", token);
            });
    }
    public sealed class ReassignStoredCommand
    {
        public byte[] Fingerprint { get; set; } = [];
        public string ResultJson { get; set; } = "";
    }
}
