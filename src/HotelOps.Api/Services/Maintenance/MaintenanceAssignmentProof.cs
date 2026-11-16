using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

// Pending physical rows alone do not prove Assigned or Pending Assignment.
internal static class MaintenanceAssignmentProof
{
    internal static bool MatchesTriage(AuditLog audit, Incident incident, IncidentSlaDto sla, IncidentEvent triage)
    {
        if (audit.Action != "UPDATE" || audit.EntityType != "incidents" || audit.AuditId <= 0
            || audit.EntityId != incident.IncidentId.ToString(CultureInfo.InvariantCulture)
            || audit.BranchId != incident.BranchId || audit.ActorUserId is not > 0
            || triage.EventType != "TRIAGED" || triage.IncidentId != incident.IncidentId
            || triage.ActorId != audit.ActorUserId || triage.OccurredAt != audit.OccurredAt
            || audit.OccurredAt < incident.DiscoveredAt || incident.PriorityId is not > 0
            || sla.UnavailableReason != null || sla.ResponseDeadlineAt == null || sla.ResolutionDeadlineAt == null) return false;
        try
        {
            using var document = JsonDocument.Parse(audit.NewValues ?? "null");
            var value = document.RootElement;
            return value.GetProperty("priority_id").GetInt64() == incident.PriorityId
                && value.GetProperty("response_deadline_at").GetDateTime() == sla.ResponseDeadlineAt
                && value.GetProperty("resolution_deadline_at").GetDateTime() == sla.ResolutionDeadlineAt;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return false; }
    }

    internal static async Task<string> ReadAsync(HotelOpsDbContext db, long workId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Owning transaction required.");
        var work = await db.MaintenanceWorks.AsNoTracking().Include(w => w.Incident)
            .SingleAsync(w => w.MaintenanceWorkId == workId, ct);
        if (work.Status != "PENDING" || work.Incident is not { Status: "OPEN" } incident
            || work.ActualStartAt != null || work.ResolvedAt != null
            || await db.WorkIntervals.AsNoTracking().AnyAsync(i => i.WorkItemKind == "MAINTENANCE" && i.WorkItemId == workId, ct))
            return "UNVERIFIED";
        var latest = await db.IncidentEvents.AsNoTracking().Where(e => e.IncidentId == incident.IncidentId && e.EventType == "TRIAGED")
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.EventId).FirstOrDefaultAsync(ct);
        if (latest == null) return "UNVERIFIED"; // Missing history is not proof of Pending Triage.
        var id = incident.IncidentId.ToString(CultureInfo.InvariantCulture);
        var audits = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "incidents" && a.EntityId == id
            && a.OccurredAt == latest.OccurredAt && a.ActorUserId == latest.ActorId && a.BranchId == incident.BranchId).ToArrayAsync(ct);
        var sla = await IncidentSlaRead.ReadAsync(db, incident.IncidentId, ct);
        if (audits.Count(a => MatchesTriage(a, incident, sla, latest)) != 1) return "UNVERIFIED";
        if (work.AssigneeId == null) return "PENDING_ASSIGNMENT";
        return await MaintenanceExecutionAccess.AllowsAsync(db, work.AssigneeId.Value, incident.BranchId, workId, ct)
            ? "ASSIGNED" : "UNVERIFIED";
    }
}
