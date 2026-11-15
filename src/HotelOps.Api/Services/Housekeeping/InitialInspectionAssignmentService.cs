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
    private async Task<bool> InitialInspectionWriterAvailable(bool execute, CancellationToken ct)
    {
        if (await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe05_assign_initial_inspection','P') IS NOT NULL
              AND OBJECT_ID('dbo.notification_operation_reservations','U') IS NOT NULL
              AND HAS_PERMS_BY_NAME('dbo.notification_operation_reservations','OBJECT','INSERT')=0
              AND HAS_PERMS_BY_NAME('dbo.notification_operation_reservations','OBJECT','UPDATE')=0
              AND HAS_PERMS_BY_NAME('dbo.notification_operation_reservations','OBJECT','DELETE')=0
              AND HAS_PERMS_BY_NAME('dbo.fe04_rework_authority_issuers','OBJECT','INSERT')=0
              AND HAS_PERMS_BY_NAME('dbo.fe04_rework_authority_issuers','OBJECT','UPDATE')=0
              AND HAS_PERMS_BY_NAME('dbo.fe04_rework_authority_issuers','OBJECT','DELETE')=0 THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) return false;
        return await CheckReinspectionWriter("FE05.UC041.INITIAL", "dbo.fe05_assign_initial_inspection", execute, ct);
    }

    public Task<RoomTypeBatchResult> AssignInitialInspectionAsync(SessionReference ticket, long sid, long branch, long task,
        string? tag, string? key, InitialInspectionAssignmentRequest input, CancellationToken ct)
    {
        if (branch <= 0 || task <= 0 || input == null || input.InspectorId <= 0 || input.ChecklistVersionId <= 0
            || !WorkflowStrongTag(tag) || !WorkflowStrongTag(input.CycleETag) || input.DueAt.Kind != DateTimeKind.Utc)
            throw new ValidationException("Exact task/cycle versions, checklist, inspector and UTC deadline are required.");
        var reason = WorkflowReason(input.Reason);
        var operation = CleaningLifecycleRules.Operation(key!);
        var fingerprint = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { contract = InitialInspectionAssignmentSource.Contract,
            actor = ticket.UserId, branch, task, tag, input.CycleETag, input.ChecklistVersionId, input.InspectorId, input.DueAt, reason }, Json));
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await HousekeepingSchema.InstalledAsync(db, token) || !await InitialInspectionWriterAvailable(true, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            var row = await db.CleaningTasks.AsNoTracking().Where(t => t.CleaningTaskId == task && t.Room.BranchId == branch
                && t.Room.IsActive && t.Room.Floor.IsActive && (t.Room.AreaId == null || t.Room.Area!.IsActive))
                .Select(t => new { t.RoomId, t.Room.RoomTypeId, t.Status }).SingleOrDefaultAsync(token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var prior = await db.Database.SqlQuery<SubmissionLedgerRow>($"""
                SELECT result_json AS Json,fingerprint AS Fingerprint,operation_key AS OperationId FROM dbo.notification_operation_reservations WITH(HOLDLOCK)
                WHERE actor_user_id={actor} AND operation_key={operation} AND completed=1 AND fingerprint_version=1
                """).SingleOrDefaultAsync(token);
            if (prior != null)
            {
                var audit = await db.AuditLogs.AsNoTracking().Where(a => a.ActorUserId == actor && a.BranchId == branch
                    && a.EntityType == "INSPECTION_ASSIGNMENT" && a.Action == "CREATE" && a.NewValues == prior.Json).Take(2).ToArrayAsync(token);
                var verified = audit.Length == 1 ? await InitialInspectionAssignmentSource.VerifyAsync(db, audit[0], token) : null;
                if (verified == null || verified.Entity.TaskId != task || verified.OperationId != operation
                    || !CryptographicOperations.FixedTimeEquals(prior.Fingerprint, fingerprint)) return new(RoomTypeBatchStatus.Duplicate);
                return new(RoomTypeBatchStatus.Success, verified.Entity, verified.Entity.SourceETag);
            }
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WITH(HOLDLOCK) WHERE actor_user_id={actor} AND operation_key={operation}").SingleAsync(token) != 0)
                return new(RoomTypeBatchStatus.Duplicate);
            var state = await CleaningState(task, token);
            if (tag != WorkflowTag(state.Revision)) return new(RoomTypeBatchStatus.Stale);
            if (state.State != "PENDING_INSPECTION" || row.Status != "IN_PROGRESS" || !await ReworkCurrentCycle(row.RoomId, task, token)
                || await db.CleaningReworks.AnyAsync(w => w.CleaningTaskId == task, token)) return new(RoomTypeBatchStatus.Inactive);
            var cycle = await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_cleaning_cycle_state WITH(HOLDLOCK) WHERE room_id={row.RoomId} AND cleaning_task_id={task}").SingleAsync(token);
            if (input.CycleETag != WorkflowTag(cycle)) return new(RoomTypeBatchStatus.Stale);
            WorkflowDue(input.DueAt, clock.GetUtcNow().UtcDateTime);
            if (await InspectorWorkedOnCycle(input.InspectorId, task, token)) return LifecycleFailure(RoomTypeBatchStatus.InvalidReference, "INSPECTOR_CLEANER_CONFLICT");
            if (await db.RoomInspections.AsNoTracking().AnyAsync(i => i.CleaningTaskId == task && i.Status != "COMPLETED" && i.Status != "CANCELLED", token)) return LifecycleFailure(RoomTypeBatchStatus.Duplicate,"INSPECTION_ALREADY_ASSIGNED");
            var version = await CurrentInitialInspectionVersion(branch, row.RoomTypeId, token);
            if (version == null || version.Id != input.ChecklistVersionId) return LifecycleFailure(RoomTypeBatchStatus.InvalidReference, "CHECKLIST_UNAVAILABLE");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                EXEC dbo.fe05_assign_initial_inspection @actor={actor},@branch={branch},@task={task},@inspector={input.InspectorId},
                  @version={version.Id},@taskRevision={state.Revision},@cycleRevision={cycle},@due={input.DueAt},@reason={reason},
                  @operation={operation},@fingerprint={fingerprint};
                """, token);
            var result = await ReadWrittenReinspectionResult<InitialInspectionAssignmentResult>(actor, operation, token);
            if (!InitialInspectionAssignmentSource.ValidResult(result) || result.TaskId != task || result.BranchId != branch
                || result.RoomId != row.RoomId || result.VersionId != version.Id || result.InspectorId != input.InspectorId || result.OperationId != operation)
                throw new InvalidOperationException("INITIAL_INSPECTION_RECEIPT_INVALID");
            return new(RoomTypeBatchStatus.Success, result, result.SourceETag);
        }, ct, additionalUsers: [input.InspectorId], permissionCode: "INSPECTION_PERFORM",
            eligibility: async token => await EligibleInspector(ticket.UserId, branch, token) && await EligibleInspector(input.InspectorId, branch, token));
    }
}
