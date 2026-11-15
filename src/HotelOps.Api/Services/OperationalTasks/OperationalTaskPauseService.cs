using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record PauseOperationalTaskRequest(long ReasonId, string? Note);
public sealed record TaskPauseReason(long Id, string Code, string Label);

public sealed partial class RoomTypeCreationService
{
    private static readonly string[] TaskPauseCodes = ["WAITING_MATERIAL", "WAITING_EQUIPMENT", "WAITING_APPROVAL", "ACCESS_BLOCKED", "SAFETY_ISSUE", "OTHER"];

    public Task<RoomTypeBatchResult> ReadTaskPauseReasonsAsync(SessionReference ticket, long sid, long branch, long id, CancellationToken ct)
        => BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await ProgressOwned(false, id, branch, actor, token)) return new(RoomTypeBatchStatus.Forbidden);
            var reasons = await db.ReferenceValues.AsNoTracking().Where(r => r.GroupCode == "TASK_PAUSE_REASON" && r.IsActive && TaskPauseCodes.Contains(r.ValueCode))
                .OrderBy(r => r.SortOrder).ThenBy(r => r.ReferenceId).Select(r => new TaskPauseReason(r.ReferenceId, r.ValueCode, r.DisplayName)).ToArrayAsync(token);
            return new(RoomTypeBatchStatus.Success, reasons);
        }, ct, requireRoomTypeSchema: false, featurePermission: token => ProgressAccess(false, id, branch, ticket.UserId, token));

    public async Task<RoomTypeBatchResult> PauseOperationalTaskAsync(SessionReference ticket, long sid, long branch, long id,
        string expected, PauseOperationalTaskRequest input, CancellationToken ct)
    {
        if (id <= 0 || input.ReasonId <= 0 || !WorkflowStrongTag(expected) || input.Note?.Length > 1000 || input.Note?.Contains('\0') == true)
            throw new ValidationException("A task, reason, single strong ETag and note up to 1000 characters are required.");
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        var time = new SqlWorkTimeTransactionParticipant(db);
        try
        {
            return await BatchAsync(ticket, sid, branch, async (actor, token) =>
            {
                if (!await ProgressSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
                var installed = await db.Database.SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID('dbo.task_pause_events','U') IS NOT NULL AND COL_LENGTH('dbo.task_pause_events','note')=2000 AND COL_LENGTH('dbo.task_pause_events','reason_code')=60 AND COL_LENGTH('dbo.task_pause_events','reason_label')=300 AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.task_pause_events') AND is_disabled=0 AND is_not_trusted=0)=3 THEN 1 ELSE 0 END AS Value").SingleAsync(token);
                if (installed != 1) return new(RoomTypeBatchStatus.SchemaRequired);
                var task = await db.OperationalTasks.FromSqlInterpolated($"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}").AsNoTracking().SingleOrDefaultAsync(token);
                if (task == null || task.BranchId != branch) return new(RoomTypeBatchStatus.NotFound);
                if (!await ProgressOwned(false, id, branch, actor, token)) return new(RoomTypeBatchStatus.Forbidden);
                var version = await ProgressRevision(false, id, token);
                if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
                if (WorkflowTag(version.Revision) != expected) return new(RoomTypeBatchStatus.Stale);
                if (task.Status != "IN_PROGRESS" || task.ActualStartAt == null || version.State != "IN_PROGRESS") return new(RoomTypeBatchStatus.Inactive);
                var reason = await db.ReferenceValues.AsNoTracking().SingleOrDefaultAsync(r => r.ReferenceId == input.ReasonId, token);
                if (reason == null || reason.GroupCode != "TASK_PAUSE_REASON" || !reason.IsActive || !TaskPauseCodes.Contains(reason.ValueCode)) return new(RoomTypeBatchStatus.InvalidReference);
                if (reason.ValueCode == "OTHER" && note == null) throw new ValidationException("OTHER requires a nonblank note.");
                var interval = await db.WorkIntervals.AsNoTracking().SingleOrDefaultAsync(i => i.UserId == actor && i.ClosedAt == null, token);
                if (interval == null || interval.BranchId != branch || interval.WorkItemKind != "TASK" || interval.WorkItemId != id) return new(RoomTypeBatchStatus.Inactive);
                var closed = await time.CloseIntervalAsync(new(actor, actor, branch, "TASK", id), interval.IntervalId, interval.RowVersion, token);
                var updated = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='PAUSED' WHERE task_id={id} AND workflow_status='IN_PROGRESS' AND row_version={version.Revision}", token);
                if (updated != 1) return new(RoomTypeBatchStatus.Stale);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET updated_at={closed.ClosedAtUtc} WHERE task_id={id}", token);
                var progress = new OperationalTaskProgress { TaskId=id, ProgressPercent=task.ProgressPercent, OldStatus=task.Status, NewStatus=task.Status,
                    Description=$"Task paused: {reason.DisplayName}." + (note == null ? "" : $" {note}"), ReportedBy=actor, ReportedAt=closed.ClosedAtUtc!.Value };
                db.OperationalTaskProgresses.Add(progress);
                await db.SaveChangesAsync(token);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_pause_events(progress_id,reason_id,reason_code,reason_label,note,interval_id) VALUES({progress.ProgressId},{reason.ReferenceId},{reason.ValueCode},{reason.DisplayName},{note},{interval.IntervalId})", token);
                await BatchAuditAsync(actor, branch, "operational_tasks", id, new { workflowState="IN_PROGRESS" },
                    new { workflowState="PAUSED", operation="PAUSE", reason.ReferenceId, reason.ValueCode, reason.DisplayName, note, interval.IntervalId, closed.ClosedAtUtc },
                    ["workflowState", "pauseReason", "workInterval"], token);
                return new(RoomTypeBatchStatus.Success, await ProgressDto(false,id,branch,"PAUSED",token), WorkflowTag((await ProgressRevision(false,id,token))!.Revision));
            }, ct, requireRoomTypeSchema:false, featurePermission:token=>ProgressAccess(false,id,branch,ticket.UserId,token),
                afterUserLocks:token=>time.LockUsersAsync([ticket.UserId],token));
        }
        catch (WorkTimeContractException error)
        {
            return new(error.Code switch {
                "TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID" => RoomTypeBatchStatus.Forbidden,
                "TIME_STALE" => RoomTypeBatchStatus.Stale,
                "TIME_WORKDAY_REQUIRED" or "TIME_STATE_CONFLICT" => RoomTypeBatchStatus.Inactive,
                _ => RoomTypeBatchStatus.Unavailable });
        }
    }
}
