using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // ASSIGNED/ACCEPTED share legacy TODO; task_progress_versions is the sole
    // workflow authority. No inference of acceptance from assignment timestamps.
    private async Task<bool> TaskEntryInstalled(CancellationToken ct) =>
        await ProgressSchema.InstalledAsync(db, ct) && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.TaskEntry.V1' AND CONVERT(nvarchar(20),value)='1')
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.task_progress_versions')
                AND name='CK_task_progress_state_entry_v1' AND is_disabled=0 AND is_not_trusted=0)
            THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(ct) == 1;

    public async Task<RoomTypeBatchResult> EnterOperationalTaskAsync(SessionReference ticket, long sid,
        long branch, long id, string expected, bool start, CancellationToken ct)
    {
        if (id <= 0 || !WorkflowStrongTag(expected))
            throw new ValidationException("A task and single strong ETag are required.");
        var time = new SqlWorkTimeTransactionParticipant(db);
        try
        {
            return await BatchAsync(ticket, sid, branch, async (actor, token) =>
            {
                if (!await TaskEntryInstalled(token)) return new(RoomTypeBatchStatus.SchemaRequired);
                var task = await db.OperationalTasks.FromSqlInterpolated(
                    $"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}")
                    .AsNoTracking().SingleOrDefaultAsync(token);
                if (task == null || task.BranchId != branch) return new(RoomTypeBatchStatus.NotFound);
                if (!await ProgressOwned(false, id, branch, actor, token)) return new(RoomTypeBatchStatus.Forbidden);
                if (!await db.Departments.AsNoTracking().AnyAsync(d => d.DepartmentId == task.DepartmentId && d.IsActive && d.BranchId == branch, token))
                    return new(RoomTypeBatchStatus.Inactive);
                var version = await ProgressRevision(false, id, token);
                if (version == null) return new(RoomTypeBatchStatus.SchemaRequired);
                if (WorkflowTag(version.Revision) != expected) return new(RoomTypeBatchStatus.Stale);
                var from = start ? "ACCEPTED" : "ASSIGNED";
                var to = start ? "IN_PROGRESS" : "ACCEPTED";
                if (version.State != from || task.Status != "TODO" || !start && task.ActualStartAt != null || task.CompletedAt != null)
                    return new(RoomTypeBatchStatus.Inactive);
                // Change workflow before the legacy parent bumps its rowversion. Every
                // later guard/failure rolls this write back with interval and audit.
                if (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status={to} WHERE task_id={id} AND row_version={version.Revision} AND workflow_status={from}", token) != 1)
                    return new(RoomTypeBatchStatus.Stale);
                var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(token);
                if (task.ActualStartAt > now) return new(RoomTypeBatchStatus.Inactive);
                long? intervalId = null;
                if (start)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET status='IN_PROGRESS' WHERE task_id={id}", token);
                    var interval = await time.OpenIntervalAsync(new(actor, actor, branch, "TASK", id), token);
                    now = interval.OpenedAtUtc;
                    intervalId = interval.IntervalId;
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET actual_start_at=COALESCE(actual_start_at,{now}),updated_at={now} WHERE task_id={id}", token);
                }
                else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_tasks SET updated_at={now} WHERE task_id={id}", token);
                db.OperationalTaskProgresses.Add(new OperationalTaskProgress
                {
                    TaskId = id, ProgressPercent = task.ProgressPercent, OldStatus = "TODO", NewStatus = start ? "IN_PROGRESS" : "TODO",
                    Description = start ? "Task started." : "Task accepted.", ReportedBy = actor, ReportedAt = now
                });
                await db.SaveChangesAsync(token);
                await BatchAuditAsync(actor, branch, "operational_tasks", id,
                    new { workflowState = from, task.ActualStartAt },
                    new { workflowState = to, operation = start ? "START" : "ACCEPT", actualStartAt = start ? task.ActualStartAt ?? now : (DateTime?)null, intervalId },
                    start ? ["workflowState", "actualStartAt", "workInterval"] : ["workflowState"], token);
                return new(RoomTypeBatchStatus.Success, await ProgressDto(false, id, branch, to, token),
                    WorkflowTag((await ProgressRevision(false, id, token))!.Revision));
            }, ct, requireRoomTypeSchema: false,
                featurePermission: token => ProgressAccess(false, id, branch, ticket.UserId, token),
                afterUserLocks: start ? token => time.LockUsersAsync([ticket.UserId], token) : null);
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
            }, new TaskEntryFailure(error.Code == "TIME_WORKDAY_REQUIRED" ? "MSG30" : error.Code == "TIME_INTERVAL_ALREADY_OPEN" ? "MSG31" : null));
        }
    }

    public sealed record TaskEntryFailure(string? MessageCode);
}
