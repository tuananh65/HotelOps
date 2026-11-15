using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // UC110 resume uses the installed progress state; the legacy task status remains
    // IN_PROGRESS. Opening the interval, state revision and audits share one transaction.
    public async Task<RoomTypeBatchResult> ResumeOperationalTaskAsync(SessionReference ticket, long sid,
        long branch, long id, string expected, CancellationToken ct)
    {
        if (id <= 0 || !WorkflowStrongTag(expected))
            throw new ValidationException("A task and single strong ETag are required.");
        var time = new SqlWorkTimeTransactionParticipant(db);
        try
        {
            return await BatchAsync(ticket, sid, branch, async (actor, token) =>
            {
                if (!await ProgressSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
                var task = await db.OperationalTasks.FromSqlInterpolated(
                    $"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}")
                    .AsNoTracking().SingleOrDefaultAsync(token);
                if (task == null || task.BranchId != branch) return new(RoomTypeBatchStatus.NotFound);
                if (!await ProgressOwned(false, id, branch, actor, token)) return new(RoomTypeBatchStatus.Forbidden);
                var version = await ProgressRevision(false, id, token);
                if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
                if (WorkflowTag(version.Revision) != expected) return new(RoomTypeBatchStatus.Stale);
                if (task.Status != "IN_PROGRESS" || task.ActualStartAt == null || version.State != "PAUSED")
                    return new(RoomTypeBatchStatus.Inactive);
                var interval = await time.OpenIntervalAsync(new(actor, actor, branch, "TASK", id), token);
                var updated = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='IN_PROGRESS' WHERE task_id={id} AND workflow_status='PAUSED' AND row_version={version.Revision}", token);
                if (updated != 1) return new(RoomTypeBatchStatus.Stale);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET updated_at={interval.OpenedAtUtc} WHERE task_id={id}", token);
                db.OperationalTaskProgresses.Add(new OperationalTaskProgress
                {
                    TaskId = id, ProgressPercent = task.ProgressPercent, OldStatus = task.Status, NewStatus = task.Status,
                    Description = "Task resumed.", ReportedBy = actor, ReportedAt = interval.OpenedAtUtc
                });
                await db.SaveChangesAsync(token);
                await BatchAuditAsync(actor, branch, "operational_tasks", id,
                    new { workflowState = "PAUSED" },
                    new { workflowState = "IN_PROGRESS", operation = "RESUME", interval.IntervalId, interval.OpenedAtUtc },
                    ["workflowState", "workInterval"], token);
                return new(RoomTypeBatchStatus.Success, await ProgressDto(false, id, branch, "IN_PROGRESS", token),
                    WorkflowTag((await ProgressRevision(false, id, token))!.Revision));
            }, ct, requireRoomTypeSchema: false,
                featurePermission: token => ProgressAccess(false, id, branch, ticket.UserId, token),
                afterUserLocks: token => time.LockUsersAsync([ticket.UserId], token));
        }
        catch (WorkTimeContractException error)
        {
            return new(error.Code switch
            {
                "TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID" => RoomTypeBatchStatus.Forbidden,
                "TIME_STALE" => RoomTypeBatchStatus.Stale,
                "TIME_WORKDAY_REQUIRED" or "TIME_INTERVAL_ALREADY_OPEN" or "TIME_STATE_CONFLICT" => RoomTypeBatchStatus.Inactive,
                "TIME_SOURCE_UNRESOLVED" => RoomTypeBatchStatus.SchemaRequired,
                _ => RoomTypeBatchStatus.Unavailable
            });
        }
    }
}
