using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Rooms;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> RejectInspectionAsync(SessionReference ticket, long sid, long branch,
        long inspection, string expected, RejectInspectionRequest input, CancellationToken ct)
    {
        if (inspection <= 0 || !WorkflowStrongTag(expected))
            throw new ValidationException("Inspection ID and strong rowversion If-Match are required.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            var loaded = await PrepareRejectInCallerTransactionAsync(ticket, sid,
                new(branch, inspection, new(input.Reason, input.Confirmed, expected)), token);
            if (loaded.Status != RoomTypeBatchStatus.Success) return new(loaded.Status);
            var source = loaded.Source!;
            var i = source.Domain.Inspection;
            var task = source.Domain.CleaningTask.CleaningTaskId;
            if ((await CleaningState(task, token)).State != "PENDING_INSPECTION" || source.Domain.CleaningTask.Status != "IN_PROGRESS")
                return new(RoomTypeBatchStatus.Inactive);
            if (await db.CleaningAssignments.AsNoTracking().AnyAsync(a => a.CleaningTaskId == task && a.AssigneeUserId == actor && a.AssignmentStatus != "CANCELLED", token)
                || await db.CleaningProgressLogs.AsNoTracking().AnyAsync(p => p.CleaningTaskId == task && p.LoggedBy == actor, token))
                return new(RoomTypeBatchStatus.Forbidden);
            // A reinspection has its own defect/lifecycle contract; this is initial conclusion only.
            if (await db.CleaningReworks.AsNoTracking().AnyAsync(r => r.CleaningTaskId == task, token))
                return new(RoomTypeBatchStatus.Unavailable);
            var room = await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={i.RoomId}")
                .AsNoTracking().SingleAsync(token);
            var readiness = new EffectiveRoomReadiness(db);
            var before = (await readiness.ReadAsync([i.RoomId], clock.GetUtcNow().UtcDateTime, token))[i.RoomId];
            var cleaner = await ReadInspectionCleanerAttributionAsync(db.Database.GetDbConnection(),
                db.Database.CurrentTransaction!.GetDbTransaction(), inspection, branch, token);
            if (cleaner == null) return new(RoomTypeBatchStatus.Unavailable);
            var supervisors = await InspectionFailureRecipients.SupervisorsAsync(db, branch, clock.GetUtcNow().UtcDateTime, token);
            var reason = input.Reason.Trim();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                SET XACT_ABORT ON;
                IF NOT EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK)
                    WHERE room_id={i.RoomId} AND cleaning_task_id={task})
                    THROW 51990,'INSPECTION_CURRENT_CYCLE_REQUIRED',1;
                UPDATE dbo.room_inspections SET workflow_status='FAILED',status='COMPLETED',result='FAIL',
                    summary_note={reason},inspected_at=SYSUTCDATETIME()
                  WHERE inspection_id={inspection} AND room_id={i.RoomId} AND cleaning_task_id={task}
                    AND inspector_user_id={actor} AND row_version={source.Domain.Revision}
                    AND workflow_status='PENDING_CONCLUSION' AND status='IN_PROGRESS' AND result IS NULL;
                IF @@ROWCOUNT<>1 THROW 51990,'INSPECTION_WRITE_CONFLICT',1;
                UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED'
                  WHERE cleaning_task_id={task} AND room_id={i.RoomId}
                    AND workflow_status='PENDING_INSPECTION' AND status='IN_PROGRESS';
                IF @@ROWCOUNT<>1 THROW 51990,'CLEANING_PHASE_CONFLICT',1;
                """, token);
            var after = (await readiness.ReadAsync([i.RoomId], clock.GetUtcNow().UtcDateTime, token))[i.RoomId];
            if (after.Readiness is not ("REWORK_REQUIRED" or "BLOCKED"))
                throw new InvalidOperationException("INSPECTION_REJECTION_READINESS_CONFLICT");
            await RoomReadinessHistory.RecordAsync(db, before, after, room.OperationalStatus, room.OperationalStatus,
                "INSPECTION", inspection, actor, clock.GetUtcNow().UtcDateTime, reason, token);
            var oldJson = JsonSerializer.Serialize(new { workflow_status = "PENDING_CONCLUSION", effectiveReadiness = before.Readiness, operationalStatus = room.OperationalStatus });
            var newJson = JsonSerializer.Serialize(new
            {
                workflow_status = "FAILED", result = "FAIL", reason, roomId = i.RoomId,
                cleaningTaskId = task, effectiveReadiness = after.Readiness, operationalStatus = room.OperationalStatus,
                defects = source.Domain.Defects.Select(d => d.DefectId).Order().ToArray(),
                notification = new InspectionFailureIntent(1, "INSPECTION_FAILED", supervisors, cleaner)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            // Keep the audit and readiness history atomic. Do not write REWORK_REQUIRED/BLOCKED
            // into the physical status/history CHECK constraints, or invent OUT_OF_SERVICE.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,old_values,new_values,occurred_at)
                VALUES({branch},{actor},'INSPECTION',CONVERT(nvarchar(50),{inspection}),'UPDATE',{oldJson},{newJson},SYSUTCDATETIME());
                """, token);
            var state = await InspectionState(inspection, token);
            var result = await ExecutionDto(i, state, branch, token);
            return new(RoomTypeBatchStatus.Success, result with { EffectiveReadiness = after.Readiness,
                Notification = "PENDING_DELIVERY", MessageCode = after.Readiness == "BLOCKED" ? "MSG17" : null }, WorkflowTag(state.Revision));
        }, ct, permissionCode: "INSPECTION_PERFORM", preserveCommitErrors: true);
    }
}
