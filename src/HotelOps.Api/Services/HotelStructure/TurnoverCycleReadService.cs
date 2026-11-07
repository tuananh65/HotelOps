using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Security.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class TurnoverHistoryService
{
    public Task<TurnoverHistoryResult> ReadTurnoverAsync(SessionReference ticket, long sid, long roomId,
        TurnoverQuery input, long? cycleId, CancellationToken ct, long? attachmentId = null, string? privateRoot = null)
    {
        var q = TurnoverReadRules.Validate(roomId, cycleId, input, attachmentId);
        return ReadAsync(ticket, sid, q.BranchId, async token =>
        {
            var branches = await HistoryBranches("CLEANING_VIEW", q.BranchId, token);
            var rooms = await HistoryRooms(token);
            if (rooms == null) return new(TurnoverHistoryStatus.SchemaRequired);
            var room = await rooms.Where(r => r.RoomId == roomId && branches.Contains(r.BranchId))
                .Select(r => new { r.RoomId, r.RoomNumber, r.BranchId }).SingleOrDefaultAsync(token);
            if (room == null) return new(TurnoverHistoryStatus.NotFound);
            var access = await permissions.EvaluateBranchAsync("INSPECTION_VIEW", room.BranchId, token);
            if (access.Decision == PermissionDecision.Unavailable) return new(TurnoverHistoryStatus.Unavailable);
            var inspect = access.IsGranted;
            // No accepted conclusion/source adapter exists. Never filter by an unaccepted PASS/FAIL value.
            if (q.Result != null) return new(TurnoverHistoryStatus.Unavailable, new { code = "INSPECTION_RESULT_SOURCE_NOT_ACCEPTED" });
            var pointer = await HasTable("room_cleaning_cycle_state", token);
            var readiness = await HasTable("room_readiness_events", token);
            var workflow = await HasColumn("cleaning_tasks", "workflow_status", token);
            var reworkWorkflow = await HasColumn("cleaning_reworks", "workflow_status", token);
            var coverage = new TurnoverCoverage("PERSISTED_SOURCE", inspect ? "NOT_ACCEPTED" : "SOURCE_READ_DENIED",
                inspect ? "PERSISTED_SOURCE_NOT_SHARED_ACCEPTED" : "SOURCE_READ_DENIED",
                readiness ? "PERSISTED_EVENTS_NOT_SHARED_ACCEPTED" : "UNAVAILABLE",
                "UNAVAILABLE_CYCLE_LINK_CONTRACT");
            var tasks = db.CleaningTasks.AsNoTracking().Where(t => t.RoomId == roomId);
            if (cycleId != null) tasks = tasks.Where(t => t.CleaningTaskId == cycleId);
            if (q.From is { } from) tasks = tasks.Where(t => t.CreatedAt >= from);
            if (q.To is { } to) tasks = tasks.Where(t => t.CreatedAt <= to);
            var total = await tasks.CountAsync(token);
            if (cycleId != null && total == 0) return new(TurnoverHistoryStatus.NotFound);
            var page = await tasks.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.CleaningTaskId)
                .Skip(cycleId == null ? (q.Page - 1) * q.PageSize : 0).Take(cycleId == null ? q.PageSize : 1).ToArrayAsync(token);
            var cycles = new List<TurnoverCycle>();
            foreach (var task in page)
            {
                var assignments = await db.CleaningAssignments.AsNoTracking().Where(a => a.CleaningTaskId == task.CleaningTaskId)
                    .OrderBy(a => a.AssignedAt).ThenBy(a => a.AssignmentId).Select(a => new TurnoverAssignee(a.AssignmentId, a.AssigneeUserId,
                        a.AssigneeUser.FullName, a.AssignmentStatus, a.AssignedAt, a.EndedAt)).ToArrayAsync(token);
                var state = workflow ? await db.Database.SqlQuery<string?>($"SELECT workflow_status AS [Value] FROM dbo.cleaning_tasks WHERE cleaning_task_id={task.CleaningTaskId}").SingleAsync(token) : null;
                bool? currentCycle = pointer ? await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.room_cleaning_cycle_state WHERE room_id={roomId} AND cleaning_task_id={task.CleaningTaskId}").SingleAsync(token) == 1 : null;
                var reworkCount = inspect ? await ValidReworks(task.CleaningTaskId, roomId).CountAsync(token) : (int?)null;
                var idKey = task.CleaningTaskId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var cancellation = task.Status == "CANCELLED" ? await db.AuditLogs.AsNoTracking().Where(a => a.BranchId == room.BranchId
                    && a.EntityType == "cleaning_tasks" && a.EntityId == idKey && a.Action == "CANCEL")
                    .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.AuditId).Select(a => a.NewValues).FirstOrDefaultAsync(token) : null;
                cycles.Add(new(task.CleaningTaskId, roomId, room.BranchId, room.RoomNumber, task.TaskNo,
                    await db.ReferenceValues.Where(r => r.ReferenceId == task.CleaningTypeId).Select(r => r.DisplayName).SingleAsync(token),
                    task.Status, state, TurnoverReadRules.Utc(task.CreatedAt), TurnoverReadRules.Utc(task.ActualStartAt), TurnoverReadRules.Utc(task.ActualEndAt),
                    currentCycle, null, reworkCount, TurnoverReadRules.CancellationReason(cancellation),
                    assignments.Select(a => a with { AssignedAtUtc = TurnoverReadRules.Utc(a.AssignedAtUtc), EndedAtUtc = TurnoverReadRules.Utc(a.EndedAtUtc) }).ToArray()));
            }
            object value = new TurnoverPage(roomId, room.BranchId, cycles, total, q, coverage);
            if (cycleId is { } cycle)
            {
                var inspections = inspect ? await db.RoomInspections.AsNoTracking().Where(i => i.RoomId == roomId && i.CleaningTaskId == cycle)
                    .OrderBy(i => i.CreatedAt).ThenBy(i => i.InspectionId).Select(i => new TurnoverInspection(i.InspectionId, cycle, roomId,
                        i.VersionId, i.Version.VersionNo, i.InspectorUserId, i.Status, i.InspectedAt ?? i.CreatedAt, null, "NOT_ACCEPTED")).ToArrayAsync(token) : [];
                var reworks = new List<TurnoverRework>();
                if (inspect)
                    foreach (var r in await ValidReworks(cycle, roomId).OrderBy(w => w.RequestedAt).ThenBy(w => w.ReworkId).ToArrayAsync(token))
                        reworks.Add(new(r.ReworkId, r.InspectionId, cycle, r.AssigneeUserId, r.Status,
                            reworkWorkflow ? await db.Database.SqlQuery<string?>($"SELECT workflow_status AS [Value] FROM dbo.cleaning_reworks WHERE rework_id={r.ReworkId}").SingleAsync(token) : null,
                            TurnoverReadRules.Utc(r.RequestedAt), TurnoverReadRules.Utc(r.CompletedAt), r.Note));
                var eventRows = Timeline(roomId, room.BranchId, cycle, readiness, inspect, await HasTable("work_intervals", token));
                var eventCount = await eventRows.CountAsync(token);
                var timeline = await eventRows.OrderBy(e => e.AtUtc).ThenBy(e => e.SourceType).ThenBy(e => e.SourceId).ThenBy(e => e.Event)
                    .Skip((q.TimelinePage - 1) * q.TimelinePageSize).Take(q.TimelinePageSize).ToArrayAsync(token);
                var inspectionIds = inspections.Select(i => i.Id).ToArray();
                var reworkFileIds = !inspect && await HasTable("fs016_rework_evidence", token)
                    ? await db.Database.SqlQuery<long>($"SELECT attachment_id AS [Value] FROM dbo.fs016_rework_evidence").ToArrayAsync(token) : [];
                var evidence = await db.Attachments.AsNoTracking().Where(a => a.DeletedAt == null
                    && !reworkFileIds.Contains(a.AttachmentId)
                    && (a.EntityType == "CLEANING_TASK" && a.EntityId == cycle || inspect && a.EntityType == "INSPECTION" && inspectionIds.Contains(a.EntityId)))
                    .OrderBy(a => a.UploadedAt).ThenBy(a => a.AttachmentId)
                    .Select(a => new HistoryEvidenceDto(a.AttachmentId, a.EntityType, a.EntityId, a.OriginalFileName ?? a.FileName, a.MimeType,
                        a.FileSizeBytes, a.UploadedAt, $"/api/rooms/{roomId}/turnover-history/{cycle}/evidence/{a.AttachmentId}?branchId={room.BranchId}")).ToArrayAsync(token);
                if (attachmentId is { } aid)
                {
                    var file = await HistoryFile(evidence, aid, privateRoot, token);
                    if (file == null) return new(TurnoverHistoryStatus.NotFound);
                    value = file;
                }
                else value = new TurnoverDetail(cycles[0], timeline.Select(e => e with { AtUtc = TurnoverReadRules.Utc(e.AtUtc) }).ToArray(),
                    eventCount, q.TimelinePage, q.TimelinePageSize, inspections.Select(i => i with { AtUtc = TurnoverReadRules.Utc(i.AtUtc) }).ToArray(),
                    reworks, evidence.Select(e => e with { UploadedAt = TurnoverReadRules.Utc(e.UploadedAt) }).ToArray(), coverage);
            }
            if (!await HistoryFinal("CLEANING_VIEW", [room.BranchId], token)) return new(TurnoverHistoryStatus.Forbidden);
            var finalInspection = await permissions.EvaluateBranchAsync("INSPECTION_VIEW", room.BranchId, token);
            if (finalInspection.Decision == PermissionDecision.Unavailable) return new(TurnoverHistoryStatus.Unavailable);
            if (inspect && !finalInspection.IsGranted) return new(TurnoverHistoryStatus.Forbidden);
            return new(TurnoverHistoryStatus.Success, value);
        }, ct);
    }
    private IQueryable<HotelOps.Api.Entities.CleaningRework> ValidReworks(long cycle, long room) => db.CleaningReworks.AsNoTracking()
        .Where(w => w.CleaningTaskId == cycle && w.CleaningTask.RoomId == room && w.Inspection.RoomId == room && w.Inspection.CleaningTaskId == cycle);
    private async Task<bool> HasTable(string table, CancellationToken ct) => await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID({"dbo." + table},'U') IS NULL THEN 0 ELSE 1 END AS [Value]").SingleAsync(ct) == 1;
    private async Task<bool> HasColumn(string table, string column, CancellationToken ct) => await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH({"dbo." + table},{column}) IS NULL THEN 0 ELSE 1 END AS [Value]").SingleAsync(ct) == 1;
    private IQueryable<TurnoverEvent> Timeline(long room, long branch, long cycle, bool readiness, bool inspect, bool intervals)
    {
        var sql = """
            SELECT CAST('CLEANING_TASK' AS nvarchar(40)) AS SourceType,t.cleaning_task_id AS SourceId,CAST('CREATED' AS nvarchar(40)) AS Event,t.created_at AS AtUtc,t.created_by AS ActorId,CAST(NULL AS nvarchar(40)) AS State,CAST(NULL AS nvarchar(max)) AS Note FROM dbo.cleaning_tasks t WHERE t.cleaning_task_id=@cycle AND t.room_id=@room
            UNION ALL SELECT 'CLEANING_TASK',t.cleaning_task_id,'ACTUAL_START',t.actual_start_at,NULL,NULL,NULL FROM dbo.cleaning_tasks t WHERE t.cleaning_task_id=@cycle AND t.actual_start_at IS NOT NULL
            UNION ALL SELECT 'CLEANING_TASK',t.cleaning_task_id,'ACTUAL_END',t.actual_end_at,NULL,NULL,NULL FROM dbo.cleaning_tasks t WHERE t.cleaning_task_id=@cycle AND t.actual_end_at IS NOT NULL
            UNION ALL SELECT 'CLEANING_ASSIGNMENT',a.assignment_id,'ASSIGNED',a.assigned_at,a.assigned_by,NULL,a.reason FROM dbo.cleaning_assignments a WHERE a.cleaning_task_id=@cycle
            UNION ALL SELECT 'CLEANING_ASSIGNMENT',a.assignment_id,'ENDED',a.ended_at,NULL,a.assignment_status,a.reason FROM dbo.cleaning_assignments a WHERE a.cleaning_task_id=@cycle AND a.ended_at IS NOT NULL
            UNION ALL SELECT 'CLEANING_PROGRESS',p.progress_id,'LOGGED',p.logged_at,p.logged_by,p.status,p.note FROM dbo.cleaning_progress_logs p WHERE p.cleaning_task_id=@cycle
            UNION ALL SELECT 'AUDIT',a.audit_id,a.action,a.occurred_at,a.actor_user_id,JSON_VALUE(a.new_values,'$.entity.state'),JSON_VALUE(a.new_values,'$.reason') FROM dbo.audit_logs a WHERE a.branch_id=@branch AND a.entity_type='cleaning_tasks' AND a.entity_id=CONVERT(varchar(30),@cycle)
            """;
        if (readiness) sql += " UNION ALL SELECT 'READINESS',e.event_id,'RECORDED',e.occurred_at,e.actor_user_id,CASE WHEN e.has_effective_block=1 THEN 'BLOCKED' ELSE e.readiness END,NULL FROM dbo.room_readiness_events e WHERE e.room_id=@room AND e.cleaning_task_id=@cycle";
        if (inspect) sql += " UNION ALL SELECT 'REWORK',w.rework_id,'REQUESTED',w.requested_at,NULL,NULL,w.reason FROM dbo.cleaning_reworks w JOIN dbo.room_inspections i ON i.inspection_id=w.inspection_id WHERE w.cleaning_task_id=@cycle AND i.cleaning_task_id=@cycle AND i.room_id=@room";
        if (inspect) sql += " UNION ALL SELECT 'REWORK_AUDIT',a.audit_id,a.action,a.occurred_at,a.actor_user_id,JSON_VALUE(a.new_values,'$.entity.state'),JSON_VALUE(a.new_values,'$.note') FROM dbo.audit_logs a JOIN dbo.cleaning_reworks w ON a.entity_id=CONVERT(varchar(30),w.rework_id) JOIN dbo.room_inspections i ON i.inspection_id=w.inspection_id WHERE a.branch_id=@branch AND a.entity_type='cleaning_reworks' AND w.cleaning_task_id=@cycle AND i.cleaning_task_id=@cycle AND i.room_id=@room";
        if (intervals)
        {
            var source = " FROM dbo.work_intervals wi WHERE wi.branch_id=@branch AND (wi.work_item_kind='CLEANING' AND wi.work_item_id=@cycle";
            if (inspect) source += " OR wi.work_item_kind='REWORK' AND EXISTS(SELECT 1 FROM dbo.cleaning_reworks w JOIN dbo.room_inspections i ON i.inspection_id=w.inspection_id WHERE w.rework_id=wi.work_item_id AND w.cleaning_task_id=@cycle AND i.cleaning_task_id=@cycle AND i.room_id=@room)";
            source += ")";
            sql += " UNION ALL SELECT 'WORK_INTERVAL',wi.interval_id,'OPENED',wi.opened_at,wi.opened_by,NULL,NULL" + source;
            sql += " UNION ALL SELECT 'WORK_INTERVAL',wi.interval_id,'CLOSED',wi.closed_at,wi.closed_by,NULL,NULL" + source + " AND wi.closed_at IS NOT NULL";
        }
        return db.Database.SqlQueryRaw<TurnoverEvent>(sql, new SqlParameter("@cycle", cycle), new SqlParameter("@room", room), new SqlParameter("@branch", branch));
    }
}
