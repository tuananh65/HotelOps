using System.ComponentModel.DataAnnotations;
using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Housekeeping;

public enum CleaningMonitorStatus { Success, SessionInvalid, Forbidden, NotFound, Unavailable }
public sealed record CleaningMonitorResult(CleaningMonitorStatus Status, object? Value = null);

/// <summary>UC037 read subset. Does not cancel work, derive room lifecycle or write business/audit data.</summary>
public sealed class CleaningMonitorService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions, TimeProvider clock)
{
    private const int TimelineLimit = 100;
    private static readonly TimeZoneInfo HotelTimezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public Task<CleaningMonitorResult> ListAsync(SessionReference ticket, long sessionId, long branchId,
        CleaningMonitorQuery input, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, HotelTimezone));
        var from = input.FromDate ?? today;
        var to = input.ToDate ?? from;
        var search = input.Search?.Trim();
        var status = input.Status?.Trim().ToUpperInvariant();
        var kind = input.Kind?.Trim().ToLowerInvariant();
        if (branchId <= 0 || input.FloorId <= 0 || input.AssigneeId <= 0 || input.PriorityId <= 0
            || input.Page < 1 || input.PageSize is < 1 or > 100 || input.Page > int.MaxValue / input.PageSize
            || from == DateOnly.MinValue || from > to || to.DayNumber - from.DayNumber > 366 || to == DateOnly.MaxValue
            || search?.Length > 150 || search?.Any(char.IsControl) == true
            || status?.Length > 40 || status?.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_')) == true
            || kind is not (null or "" or "cleaning" or "rework"))
            throw new ValidationException("Invalid cleaning monitor filters.");
        var start = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), HotelTimezone);
        var end = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), HotelTimezone);
        return ReadAsync(ticket, sessionId, branchId, async token =>
        {
            var rows = (await RowsAsync(branchId, now, token)).Where(x => x.OccurredAt >= start && x.OccurredAt < end);
            if (input.FloorId is { } floor) rows = rows.Where(x => x.FloorId == floor);
            if (input.AssigneeId is { } assignee) rows = rows.Where(x => x.AssigneeId == assignee);
            if (input.PriorityId is { } priority) rows = rows.Where(x => x.PriorityId == priority);
            if (!string.IsNullOrEmpty(status)) rows = rows.Where(x => x.RecordedStatus == status || x.WorkflowState == status);
            if (!string.IsNullOrEmpty(kind)) rows = rows.Where(x => x.Kind == kind);
            if (!string.IsNullOrEmpty(search)) rows = rows.Where(x => x.TaskNumber.Contains(search)
                || x.RoomNumber.Contains(search) || (x.AssigneeName != null && x.AssigneeName.Contains(search))
                || (x.Description != null && x.Description.Contains(search)));
            if (input.Overdue is true) rows = rows.Where(x => x.DueAt != null && x.DueAt < now
                && (x.WorkflowState ?? x.RecordedStatus) != "COMPLETED" && (x.WorkflowState ?? x.RecordedStatus) != "CANCELLED");
            if (input.Overdue is false) rows = rows.Where(x => (x.WorkflowState ?? x.RecordedStatus) == "COMPLETED"
                || (x.WorkflowState ?? x.RecordedStatus) == "CANCELLED" || (x.DueAt != null && x.DueAt >= now));
            var total = await rows.CountAsync(token);
            var page = await rows.OrderBy(x => x.DueAt == null).ThenBy(x => x.DueAt).ThenBy(x => x.Id).ThenBy(x => x.Kind)
                .Skip((input.Page - 1) * input.PageSize).Take(input.PageSize).ToListAsync(token);
            return new(CleaningMonitorStatus.Success,
                new CleaningMonitorPage(page.Select(x => Item(x, now)).ToArray(), total, input.Page, input.PageSize,
                    from, to, HotelTimezone.Id, now));
        }, ct);
    }

    public Task<CleaningMonitorResult> DetailAsync(SessionReference ticket, long sessionId, long branchId,
        string kind, long id, CancellationToken ct)
        => DetailAsync(ticket, sessionId, branchId, kind, id, new CleaningMonitorTimelineQuery(), ct);

    public Task<CleaningMonitorResult> DetailAsync(SessionReference ticket, long sessionId, long branchId,
        string kind, long id, CleaningMonitorTimelineQuery input, CancellationToken ct)
    {
        if (branchId <= 0 || id <= 0 || kind is not ("cleaning" or "rework"))
            throw new ValidationException("Invalid cleaning monitor identifier.");
        if (input.TimelinePage < 1 || input.TimelinePageSize is < 1 or > TimelineLimit
            || input.TimelinePage > int.MaxValue / input.TimelinePageSize)
            throw new ValidationException("Invalid cleaning monitor timeline pagination.");
        var skip = (input.TimelinePage - 1) * input.TimelinePageSize;
        var now = clock.GetUtcNow().UtcDateTime;
        return ReadAsync(ticket, sessionId, branchId, async token =>
        {
            var row = await (await RowsAsync(branchId, now, token)).SingleOrDefaultAsync(x => x.Kind == kind && x.Id == id, token);
            if (row is null) return new(CleaningMonitorStatus.NotFound);
            var assignments = db.CleaningAssignments.AsNoTracking().Where(x => x.CleaningTaskId == row.CleaningTaskId);
            var assignmentCount = await assignments.CountAsync(token);
            var assignmentRows = await assignments.OrderByDescending(x => x.AssignedAt).ThenByDescending(x => x.AssignmentId)
                .Skip(skip).Take(input.TimelinePageSize).Select(x => new MonitorAssignment(x.AssignmentId, x.AssigneeUserId,
                    x.AssignmentStatus, x.AssignedAt, x.EndedAt, x.Reason)).ToListAsync(token);
            var progress = db.CleaningProgressLogs.AsNoTracking().Where(x => x.CleaningTaskId == row.CleaningTaskId);
            var progressCount = await progress.CountAsync(token);
            var progressRows = await progress.OrderByDescending(x => x.LoggedAt).ThenByDescending(x => x.ProgressId)
                .Skip(skip).Take(input.TimelinePageSize).Select(x => new MonitorProgress(x.ProgressId, x.ProgressPercent, x.Status,
                    x.BlockerType == null ? null : x.BlockerType.ValueCode, x.Note, x.LoggedBy, x.LoggedAt)).ToListAsync(token);
            var taskKey = row.CleaningTaskId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var itemKey = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var audit = db.AuditLogs.AsNoTracking().Where(x => x.BranchId == branchId
                && ((x.EntityType == "cleaning_tasks" && x.EntityId == taskKey)
                    || (kind == "rework" && x.EntityType == "cleaning_reworks" && x.EntityId == itemKey)));
            var auditCount = await audit.CountAsync(token);
            var auditRows = await audit.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.AuditId)
                .Skip(skip).Take(input.TimelinePageSize).Select(x => new MonitorAudit(x.AuditId, x.EntityType, x.Action,
                    x.ActorUserId, x.OccurredAt)).ToListAsync(token);
            return new(CleaningMonitorStatus.Success, new CleaningMonitorDetail(Item(row, now),
                assignmentRows.Select(x => x with { AssignedAtUtc = Utc(x.AssignedAtUtc), EndedAtUtc = Utc(x.EndedAtUtc) }).ToArray(),
                assignmentCount, progressRows.Select(x => x with { LoggedAtUtc = Utc(x.LoggedAtUtc) }).ToArray(),
                progressCount, auditRows.Select(x => x with { OccurredAtUtc = Utc(x.OccurredAtUtc) }).ToArray(),
                auditCount, input.TimelinePageSize, now, input.TimelinePage));
        }, ct);
    }

    private async Task<CleaningMonitorResult> ReadAsync(SessionReference ticket, long sessionId, long branchId,
        Func<CancellationToken, Task<CleaningMonitorResult>> read, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted || actor.UserId != ticket.UserId
            || actor.SessionId != sessionId || ticket.IsRestricted) return new(CleaningMonitorStatus.SessionInvalid);
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Monitor owns its read transaction.");
        try
        {
            // Retain session/permission read locks through count, page and detail; no business mutations.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await Gate() is { } denied) return new(denied);
            var result = await read(ct);
            if (await Gate() is { } changed) return new(changed);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (SqlException error) when (!ct.IsCancellationRequested && error.Number is -2 or 1205 or 1222 or 207 or 208)
        { return new(CleaningMonitorStatus.Unavailable); }

        async Task<CleaningMonitorStatus?> Gate()
        {
            var valid = await sessions.ValidateSessionAsync(ticket, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted)
                return CleaningMonitorStatus.SessionInvalid;
            // Supervisor monitor requires an actual branch/global view grant. OWN is not promoted to branch access.
            var access = await permissions.EvaluateBranchAsync("CLEANING_VIEW", branchId, ct);
            return access.IsGranted ? null : access.Decision == PermissionDecision.Unavailable
                ? CleaningMonitorStatus.Unavailable : CleaningMonitorStatus.Forbidden;
        }
    }

    private async Task<IQueryable<MonitorRow>> RowsAsync(long branchId, DateTime now, CancellationToken ct)
    {
        // Read optional Task73 columns without changing generated entities or shared EF mappings.
        var cleaningWorkflow = await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NULL THEN 0 ELSE 1 END AS [Value]").SingleAsync(ct);
        var reworkWorkflow = await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH('dbo.cleaning_reworks','workflow_status') IS NULL THEN 0 ELSE 1 END AS [Value]").SingleAsync(ct);
        var reworkDue = await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH('dbo.cleaning_reworks','due_at') IS NULL THEN 0 ELSE 1 END AS [Value]").SingleAsync(ct);
        // Interpolated fragments are fixed identifiers selected only by metadata, never client text.
        var sql = $"""
            SELECT CAST('cleaning' AS nvarchar(10)) AS Kind, c.cleaning_task_id AS Id,
                c.cleaning_task_id AS CleaningTaskId,c.task_no AS TaskNumber,r.room_id AS RoomId,r.room_number AS RoomNumber,
                r.floor_id AS FloorId,f.floor_name AS FloorName,c.status AS RecordedStatus,
                {(cleaningWorkflow == 1 ? "c.workflow_status" : "CAST(NULL AS nvarchar(40))")} AS WorkflowState,
                c.priority_id AS PriorityId,p.value_code AS PriorityCode,a.assignee_user_id AS AssigneeId,u.full_name AS AssigneeName,
                c.due_at AS DueAt,COALESCE(c.scheduled_start_at,c.created_at) AS OccurredAt,
                g.progress_percent AS ProgressPercent,b.value_code AS BlockerCode,g.note AS BlockerNote,c.instruction AS Description
            FROM dbo.cleaning_tasks c JOIN dbo.rooms r ON r.room_id=c.room_id
            LEFT JOIN dbo.floors f ON f.floor_id=r.floor_id AND f.branch_id=r.branch_id
            LEFT JOIN dbo.reference_values p ON p.reference_id=c.priority_id
            OUTER APPLY(SELECT TOP(1) ca.assignee_user_id FROM dbo.cleaning_assignments ca
                WHERE ca.cleaning_task_id=c.cleaning_task_id AND ca.assignment_status='ACTIVE' AND ca.ended_at IS NULL
                AND ca.assigned_at<=@now ORDER BY ca.assigned_at DESC,ca.assignment_id DESC) a
            LEFT JOIN dbo.users u ON u.user_id=a.assignee_user_id
            OUTER APPLY(SELECT TOP(1) cl.progress_percent,cl.blocker_type_id,cl.note FROM dbo.cleaning_progress_logs cl
                WHERE cl.cleaning_task_id=c.cleaning_task_id ORDER BY cl.logged_at DESC,cl.progress_id DESC) g
            LEFT JOIN dbo.reference_values b ON b.reference_id=g.blocker_type_id
            WHERE r.branch_id=@branch
            UNION ALL
            SELECT CAST('rework' AS nvarchar(10)),w.rework_id,c.cleaning_task_id,c.task_no,r.room_id,r.room_number,
                r.floor_id,f.floor_name,w.status,
                {(reworkWorkflow == 1 ? "w.workflow_status" : "CAST(NULL AS nvarchar(40))")},
                c.priority_id,p.value_code,w.assignee_user_id,u.full_name,
                {(reworkDue == 1 ? "w.due_at" : "CAST(NULL AS datetime2)")},w.requested_at,
                CAST(NULL AS int),CAST(NULL AS varchar(50)),CAST(NULL AS nvarchar(max)),w.reason
            FROM dbo.cleaning_reworks w JOIN dbo.cleaning_tasks c ON c.cleaning_task_id=w.cleaning_task_id
            JOIN dbo.rooms r ON r.room_id=c.room_id
            JOIN dbo.room_inspections i ON i.inspection_id=w.inspection_id AND i.room_id=r.room_id AND i.cleaning_task_id=c.cleaning_task_id
            LEFT JOIN dbo.floors f ON f.floor_id=r.floor_id AND f.branch_id=r.branch_id
            LEFT JOIN dbo.reference_values p ON p.reference_id=c.priority_id
            LEFT JOIN dbo.users u ON u.user_id=w.assignee_user_id
            WHERE r.branch_id=@branch
            """;
        return db.Database.SqlQueryRaw<MonitorRow>(sql, new SqlParameter("@branch", branchId),
            new SqlParameter("@now", SqlDbType.DateTime2) { Value = now });
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value is { } date ? Utc(date) : null;
    private static CleaningMonitorItem Item(MonitorRow row, DateTime now) => new(row.Kind, row.Id,
        row.CleaningTaskId, row.TaskNumber, row.RoomId, row.RoomNumber, row.FloorId, row.FloorName,
        row.RecordedStatus, row.WorkflowState, row.PriorityId, row.PriorityCode, row.AssigneeId, row.AssigneeName,
        Utc(row.DueAt), (row.WorkflowState ?? row.RecordedStatus) is "COMPLETED" or "CANCELLED"
            ? false : row.DueAt is null ? null : row.DueAt < now,
        row.ProgressPercent, row.BlockerCode, row.BlockerNote, row.Description);

    private sealed class MonitorRow
    {
        public string Kind { get; set; } = "";
        public long Id { get; set; }
        public long CleaningTaskId { get; set; }
        public string TaskNumber { get; set; } = "";
        public long RoomId { get; set; }
        public string RoomNumber { get; set; } = "";
        public long FloorId { get; set; }
        public string? FloorName { get; set; }
        public string RecordedStatus { get; set; } = "";
        public string? WorkflowState { get; set; }
        public long? PriorityId { get; set; }
        public string? PriorityCode { get; set; }
        public long? AssigneeId { get; set; }
        public string? AssigneeName { get; set; }
        public DateTime? DueAt { get; set; }
        public DateTime OccurredAt { get; set; }
        public int? ProgressPercent { get; set; }
        public string? BlockerCode { get; set; }
        public string? BlockerNote { get; set; }
        public string? Description { get; set; }
    }
}
