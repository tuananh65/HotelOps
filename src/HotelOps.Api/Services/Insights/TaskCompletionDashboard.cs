using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal static class TaskCompletionDashboard
{
    internal sealed class ReceiptCount
    {
        public long TaskId { get; set; }
        public int Accepts { get; set; }
        public int Reopens { get; set; }
    }

    internal static async Task AppendAsync(HotelOpsDbContext db, InsightFilters filters, DateTime now,
        List<MetricRow> metrics, List<string> missing, CancellationToken ct)
    {
        if (!await TaskReopenStore.InstalledAsync(db, ct)) { Unavailable("cycle receipt capability unavailable"); return; }
        var start = InsightAccess.Start(filters);
        var end = InsightAccess.End(filters);
        var scoped = db.OperationalTasks.AsNoTracking().Where(t => filters.BranchIds.Contains(t.BranchId)
            && (filters.DepartmentId == null || t.DepartmentId == filters.DepartmentId));
        var workflow = TaskWorkflowRead.Query(db, now);
        // Unresolved open lifecycles do not erase a proven completion metric.
        // Every possible completed candidate must, however, resolve to canonical Closed.
        if (await scoped.AnyAsync(t => (t.Status == "COMPLETED" || t.CompletedAt != null)
            && !workflow.Any(w => w.TaskId == t.TaskId && w.State == "CLOSED"), ct))
        { Unavailable("inconsistent canonical completion population"); return; }
        var tasks = await (from task in db.OperationalTasks.AsNoTracking()
            join state in TaskWorkflowRead.Query(db, now) on task.TaskId equals state.TaskId
            where filters.BranchIds.Contains(task.BranchId) && (filters.DepartmentId == null || task.DepartmentId == filters.DepartmentId)
                && state.State == "CLOSED" && task.CompletedAt >= start && task.CompletedAt < end && task.CompletedAt <= now
            select new { task.TaskId, task.CompletedAt, task.BranchId,
                ValidLocation = task.Department.BranchId == task.BranchId
                    && (task.RoomId == null || task.Room!.BranchId == task.BranchId)
                    && (task.AreaId == null || task.Area!.BranchId == task.BranchId) }).Take(50001).ToArrayAsync(ct);
        if (tasks.Length > 50000) { Unavailable("task validation limit exceeded; narrow the period"); return; }
        if (tasks.Length == 0) { Add(null); return; }
        if (tasks.Any(t => !t.ValidLocation)) { Unavailable("inconsistent task location provenance"); return; }
        var ids = tasks.Select(t => t.TaskId).ToArray();
        var accepts = await TaskVerificationStore.Query(db).Where(v => ids.Contains(v.TaskId) && v.Decision == "ACCEPT").Take(50001).ToArrayAsync(ct);
        var submissions = await TaskSubmissionStore.Query(db).Where(s => ids.Contains(s.TaskId)).Take(50001).ToArrayAsync(ct);
        var reopens = await TaskReopenStore.Query(db).Where(r => ids.Contains(r.TaskId)).Take(50001).ToArrayAsync(ct);
        var intervals = await db.WorkIntervals.AsNoTracking().Where(i => i.WorkItemKind == "TASK" && ids.Contains(i.WorkItemId)).Take(50001).ToArrayAsync(ct);
        if (accepts.Length > 50000 || submissions.Length > 50000 || reopens.Length > 50000 || intervals.Length > 50000)
        { Unavailable("cycle validation limit exceeded; narrow the period"); return; }
        // Compare raw immutable event counts with provenance-validated readers: an invalid
        // historical receipt must not disappear and turn a multi-cycle task into one cycle.
        var counts = await db.Database.SqlQuery<ReceiptCount>($"""
            SELECT t.task_id AS TaskId,
              (SELECT COUNT(*) FROM dbo.task_verification_events v JOIN dbo.task_submission_events s ON s.progress_id=v.submission_id WHERE s.task_id=t.task_id AND v.decision='ACCEPT') AS Accepts,
              (SELECT COUNT(*) FROM dbo.task_reopen_events r WHERE r.task_id=t.task_id) AS Reopens
            FROM dbo.operational_tasks t
            """).Where(r => ids.Contains(r.TaskId)).ToDictionaryAsync(r => r.TaskId, ct);
        var startIds = await db.Database.SqlQuery<long>($"""
            SELECT DISTINCT i.interval_id AS Value FROM dbo.work_intervals i
            JOIN dbo.audit_logs a ON a.entity_type='operational_tasks' AND a.entity_id=CONVERT(nvarchar(50),i.work_item_id)
              AND a.branch_id=i.branch_id AND a.actor_user_id=i.user_id AND a.action='UPDATE'
            CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
            WHERE i.work_item_kind='TASK' AND JSON_VALUE(j.payload,'$.operation')='START'
              AND JSON_VALUE(j.payload,'$.workflowState')='IN_PROGRESS'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.intervalId'))=i.interval_id
            """).Where(id => db.WorkIntervals.Any(i => i.IntervalId == id && ids.Contains(i.WorkItemId))).ToArrayAsync(ct);
        var starts = startIds.ToHashSet();
        var acceptsByTask = accepts.ToLookup(v => v.TaskId);
        var reopensByTask = reopens.ToLookup(r => r.TaskId);
        var submissionsByTask = submissions.ToLookup(s => s.TaskId);
        var intervalsByTask = intervals.ToLookup(i => i.WorkItemId);
        decimal total = 0;
        foreach (var task in tasks)
        {
            var verified = acceptsByTask[task.TaskId].ToArray();
            var reopened = reopensByTask[task.TaskId].ToArray();
            var submitted = submissionsByTask[task.TaskId].ToDictionary(s => s.ProgressId);
            if (!counts.TryGetValue(task.TaskId, out var count) || count.Accepts != verified.Length || count.Reopens != reopened.Length
                || intervalsByTask[task.TaskId].Any(i => i.BranchId != task.BranchId)
                || verified.Any(v => !submitted.ContainsKey(v.SubmissionId)))
            { Unavailable("missing or inconsistent immutable cycle receipts"); return; }
            var seconds = TaskCycleDuration.Seconds(task.CompletedAt!.Value,
                verified.Select(v => new TaskCycleDuration.Acceptance(v.SubmissionId, submitted[v.SubmissionId].IntervalId, submitted[v.SubmissionId].SubmittedAt, v.DecidedAt)).ToArray(),
                reopened.Select(r => new TaskCycleDuration.Reopen(r.PreviousSubmissionId, r.IntervalCutoff, r.ReopenedAt)).ToArray(),
                intervalsByTask[task.TaskId].Select(i => new TaskCycleDuration.Interval(i.IntervalId, i.OpenedAt, i.ClosedAt, starts.Contains(i.IntervalId))).ToArray());
            if (seconds == null) { Unavailable("missing or inconsistent Start/Submit processing-cycle boundaries"); return; }
            total += seconds.Value;
        }
        Add(total / tasks.Length / 3600m);

        void Unavailable(string reason) { missing.Add("KPI-05: " + reason); Add(null); }
        void Add(decimal? hours) => metrics.Add(new("averageTaskCompletionHours", "Average task completion time", hours, "hours",
            "KPI-05: sum of first Start to accepted Submit per cycle, averaged once per current Closed task in selected UTC period"));
    }
}
