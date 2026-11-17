using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.PreventiveMaintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Insights;

internal static class ReportPreventiveOccurrences
{
    internal sealed record Row(long RunId, long ScheduleId, long BranchId, long? DepartmentId, long? WorkId,
        long? AssetId, long? RoomId, long? AreaId, long? AssigneeId, string? WorkflowStatus, DateTime DueAt,
        DateTime? PerformedAt, string? Result, bool? Overdue, string? MissingReason);
    private sealed class Detail
    {
        public long RunId { get; set; }
        public long BranchId { get; set; }
        public long? AssigneeId { get; set; }
        public string WorkflowStatus { get; set; } = "";
        public string Definition { get; set; } = "";
    }
    private sealed record Definition(int SchemaVersion, long ScheduleId, long? AssetId, long? RoomId, long? AreaId, DateTime ScheduledDueAt);
    internal static async Task<Row[]> ReadAsync(HotelOpsDbContext db, InsightFilters filters, DateTime now, CancellationToken ct)
    {
        var start = InsightAccess.Start(filters); var end = InsightAccess.End(filters);
        var runs = await db.PreventiveMaintenanceRuns.AsNoTracking()
            .Where(r => r.PlannedAt >= start && r.PlannedAt < end
                && (filters.DepartmentId == null || r.Schedule.AssignedDepartmentId == filters.DepartmentId))
            .Select(r => new { r.RunId, r.ScheduleId, r.MaintenanceWorkId, r.PlannedAt, r.PerformedAt, r.PerformedBy, r.Result,
                r.Schedule.AssignedDepartmentId, r.Schedule.AssetId, r.Schedule.RoomId, r.Schedule.AreaId,
                DepartmentBranch = r.Schedule.AssignedDepartment == null ? (long?)null : r.Schedule.AssignedDepartment.BranchId,
                BranchId = r.Schedule.AssetId != null && r.Schedule.RoomId == null && r.Schedule.AreaId == null ? (long?)r.Schedule.Asset!.BranchId
                    : r.Schedule.AssetId == null && r.Schedule.RoomId != null && r.Schedule.AreaId == null ? r.Schedule.Room!.BranchId
                    : r.Schedule.AssetId == null && r.Schedule.RoomId == null && r.Schedule.AreaId != null ? r.Schedule.Area!.BranchId : null })
            .Where(r => r.BranchId != null && filters.BranchIds.Contains(r.BranchId.Value)
                && (r.AssignedDepartmentId == null || r.DepartmentBranch == r.BranchId))
            .OrderBy(r => r.PlannedAt).ThenBy(r => r.RunId).Take(50001).ToArrayAsync(ct);
        if (runs.Length > 50000) throw new InsightException(422, "REPORT_ROW_LIMIT", "Narrow the occurrence period.");
        var ids = runs.Select(r => r.RunId).ToArray();
        var details = await PreventiveOccurrenceGeneration.InstalledAsync(db, ct)
            ? await db.Database.SqlQueryRaw<Detail>("""
                SELECT run_id AS RunId,branch_id AS BranchId,assignee_id AS AssigneeId,
                  workflow_status AS WorkflowStatus,definition_json AS Definition FROM dbo.preventive_occurrence_details
                """).Where(d => ids.Contains(d.RunId) && filters.BranchIds.Contains(d.BranchId)).ToDictionaryAsync(d => d.RunId, ct)
            : new Dictionary<long, Detail>();
        var rows = new List<Row>();
        foreach (var run in runs)
        {
            string? state = null, reason = null;
            long? assignee = null;
            if (!details.TryGetValue(run.RunId, out var detail)) reason = "OCCURRENCE_SNAPSHOT_UNAVAILABLE";
            else
            {
                try
                {
                    var snapshot = JsonSerializer.Deserialize<Definition>(detail.Definition, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.ScheduleId != run.ScheduleId
                        || snapshot.ScheduledDueAt != run.PlannedAt || snapshot.AssetId != run.AssetId
                        || snapshot.RoomId != run.RoomId || snapshot.AreaId != run.AreaId || detail.BranchId != run.BranchId
                        || detail.WorkflowStatus is not ("PENDING_ASSIGNMENT" or "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD" or "COMPLETED" or "CANCELLED"))
                        reason = "OCCURRENCE_SNAPSHOT_INVALID";
                    else { state = detail.WorkflowStatus; assignee = detail.AssigneeId; }
                }
                catch (JsonException) { reason = "OCCURRENCE_SNAPSHOT_INVALID"; }
            }
            if (state == "COMPLETED")
            {
                if (!await PreventiveOccurrenceExecutionService.CompletionInstalledAsync(db, ct)) reason = "OCCURRENCE_COMPLETION_STORAGE_UNAVAILABLE";
                else if (run.PerformedAt == null || run.PerformedAt > now || !await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.preventive_occurrence_completions c
                    JOIN dbo.audit_logs a ON a.audit_id=c.audit_id
                    JOIN dbo.work_intervals w ON w.interval_id=c.interval_id
                    WHERE c.run_id={run.RunId} AND c.completed_at={run.PerformedAt} AND c.actor_id={run.PerformedBy}
                      AND a.entity_type='preventive_maintenance_runs' AND a.entity_id=CONVERT(varchar(30),c.run_id)
                      AND a.branch_id={run.BranchId} AND a.actor_user_id=c.actor_id AND a.occurred_at=c.completed_at
                      AND w.work_item_kind='OCCURRENCE' AND w.work_item_id=c.run_id AND w.branch_id={run.BranchId}
                      AND w.user_id=c.actor_id AND w.closed_at=c.completed_at AND w.closed_by=c.actor_id
                    """).AnyAsync(count => count == 1, ct)) reason = "OCCURRENCE_COMPLETION_PROOF_UNAVAILABLE";
                if (reason != null) state = null;
            }
            rows.Add(new(run.RunId, run.ScheduleId, run.BranchId!.Value, run.AssignedDepartmentId, run.MaintenanceWorkId,
                run.AssetId, run.RoomId, run.AreaId, assignee, state, run.PlannedAt, run.PerformedAt, run.Result,
                state == null ? null : run.PlannedAt < now && state is not ("COMPLETED" or "CANCELLED"), reason));
        }
        return rows.ToArray();
    }
}
