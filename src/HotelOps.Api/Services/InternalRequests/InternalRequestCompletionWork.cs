using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestCompletionBlocker(string Kind, long Id, string Code);

/// <summary>
/// Server-side prerequisite inspection for UC120, not a read authorization or a complete closure verdict.
/// Caller must authorize the request and run this in the same Serializable transaction as closure.
/// Do not expose target IDs to the UI without independent target read authorization.
/// Material linkage, escalation closure, result/audit and notification remain separate requirements.
/// </summary>
public static class InternalRequestCompletionWork
{
    public static async Task<IReadOnlyList<InternalRequestCompletionBlocker>> ReadKnownBlockersAsync(
        HotelOpsDbContext db, long requestId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Completion prerequisites require the closure Serializable transaction.");
        if (!await InternalRequestTaskLinks.InstalledAsync(db, ct)
            || !await InternalRequestCleaningLinks.InstalledAsync(db, ct)
            || !await InternalRequestIncidentLinks.InstalledAsync(db, ct)
            || !await InternalRequestHandoverStore.InstalledAsync(db, ct)
            || !await ProgressSchema.InstalledAsync(db, ct)
            || !await HousekeepingSchema.InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "IR_COMPLETION_WORK_SCHEMA_REQUIRED");

        var request = await db.InternalRequests.FromSqlInterpolated(
            $"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE internal_request_id={requestId}")
            .AsNoTracking().SingleOrDefaultAsync(ct)
            ?? throw new InternalRequestReadException(404, "IR_NOT_FOUND");
        var blockers = new List<InternalRequestCompletionBlocker>();
        var tasks = db.Database.SqlQuery<Link>($"SELECT task_id AS Id,is_mandatory AS Mandatory FROM dbo.internal_request_task_links WHERE internal_request_id={requestId}");
        // Inspect all links, never only the caller's TASK_VIEW-filtered list. A missing/unknown
        // authoritative workflow state cannot make a mandatory task appear finished.
        var states = TaskWorkflowRead.Query(db, DateTime.UtcNow);
        var taskRows = await (from link in tasks
            join task in db.OperationalTasks.AsNoTracking() on link.Id equals task.TaskId
            join state in states on task.TaskId equals state.TaskId into workflow
            from state in workflow.DefaultIfEmpty()
            select new { link.Id, link.Mandatory, task.BranchId, State = state == null ? null : state.State }).ToArrayAsync(ct);
        foreach (var row in taskRows)
            if (row.BranchId != request.BranchId || row.Mandatory && row.State != "CLOSED")
                blockers.Add(new("TASK", row.Id, row.BranchId != request.BranchId ? "LINK_BRANCH_MISMATCH" : "MANDATORY_WORK_NOT_CLOSED"));
        var explicitTaskIds = tasks.Select(l => l.Id);
        foreach (var id in await db.OperationalTasks.AsNoTracking()
            .Where(t => t.SourceType == "INTERNAL_REQUEST" && t.SourceId == requestId && !explicitTaskIds.Contains(t.TaskId))
            .Select(t => t.TaskId).ToArrayAsync(ct))
            blockers.Add(new("TASK", id, "LEGACY_COMPLETION_REQUIREMENT_UNCLASSIFIED"));

        var cleaning = await db.Database.SqlQuery<Work>($"""
            SELECT l.cleaning_task_id AS Id,l.is_mandatory AS Mandatory,r.branch_id AS BranchId,
              CASE WHEN t.workflow_status='COMPLETED' AND t.status='COMPLETED' AND t.actual_end_at IS NOT NULL
                THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS Closed
            FROM dbo.internal_request_cleaning_links l
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=l.cleaning_task_id
            JOIN dbo.rooms r ON r.room_id=t.room_id
            WHERE l.internal_request_id={requestId}
            """).ToArrayAsync(ct);
        foreach (var row in cleaning)
            if (row.BranchId != request.BranchId || row.Mandatory && !row.Closed)
                blockers.Add(new("CLEANING", row.Id, row.BranchId != request.BranchId ? "LINK_BRANCH_MISMATCH" : "MANDATORY_WORK_NOT_CLOSED"));

        var incidents = db.Database.SqlQuery<Link>($"SELECT incident_id AS Id,is_mandatory AS Mandatory FROM dbo.internal_request_incident_links WHERE internal_request_id={requestId}");
        var incidentRows = await (from link in incidents
            join incident in db.Incidents.AsNoTracking() on link.Id equals incident.IncidentId
            select new { link.Id, link.Mandatory, incident.BranchId, incident.Status, incident.ClosedAt }).ToArrayAsync(ct);
        foreach (var row in incidentRows)
            if (row.BranchId != request.BranchId || row.Mandatory && (row.Status != "CLOSED" || row.ClosedAt == null))
                blockers.Add(new("INCIDENT", row.Id, row.BranchId != request.BranchId ? "LINK_BRANCH_MISMATCH" : "MANDATORY_WORK_NOT_CLOSED"));
        if (request.RelatedIncidentId is {} legacyIncident && !await incidents.AnyAsync(l => l.Id == legacyIncident, ct))
            blockers.Add(new("INCIDENT", legacyIncident, "LEGACY_COMPLETION_REQUIREMENT_UNCLASSIFIED"));

        foreach (var id in await InternalRequestHandoverStore.Rows(db)
            .Where(h => h.RequestId == requestId && h.State == "PENDING_ACKNOWLEDGMENT").Select(h => h.Id).ToArrayAsync(ct))
            blockers.Add(new("HANDOVER", id, "HANDOVER_PENDING"));
        return blockers.OrderBy(b => b.Kind, StringComparer.Ordinal).ThenBy(b => b.Id).ToArray();
    }

    private sealed class Link { public long Id { get; set; } public bool Mandatory { get; set; } }
    private sealed class Work
    {
        public long Id { get; set; }
        public bool Mandatory { get; set; }
        public long BranchId { get; set; }
        public bool Closed { get; set; }
    }
}
