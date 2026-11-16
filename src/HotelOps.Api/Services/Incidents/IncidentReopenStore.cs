using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Incidents;

// UC060 persistence participant, not an authorization boundary. The owning workflow
// validates session, scoped permission, ETag, recurrence evidence and SYS-PAR-19.
internal static class IncidentReopenStore
{
    internal sealed class Boundary
    {
        public long EventId { get; set; }
        public long IncidentId { get; set; }
        public long PreviousWorkId { get; set; }
        public long ClosureEventId { get; set; }
        public long ClosureAuditId { get; set; }
        public long AuditId { get; set; }
        public DateTime DiscoveredAt { get; set; }
        public DateTime ClosedAt { get; set; }
        public DateTime ReopenedAt { get; set; }
        public DateTime PreviousResponseDeadlineAt { get; set; }
        public DateTime PreviousResolutionDeadlineAt { get; set; }
    }

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.IncidentReopenCycles.V1' AND CONVERT(nvarchar(10),value)='1')
              AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incident_reopen_cycles') AND is_nullable=0
                AND ((name IN('reopen_event_id','incident_id','previous_work_id','closure_event_id','closure_audit_id','reopen_audit_id') AND system_type_id=127)
                OR (name IN('recurrence_discovered_at','previous_closed_at','reopened_at','previous_response_deadline_at','previous_resolution_deadline_at') AND system_type_id=42 AND scale=7)))=11
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.incident_reopen_cycles')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0 AND update_referential_action=0
                AND name IN('FK_incident_reopen_incident','FK_incident_reopen_event','FK_incident_reopen_closure','FK_incident_reopen_work','FK_incident_reopen_closure_audit','FK_incident_reopen_audit'))=6
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID('dbo.incident_reopen_cycles')
                AND name='TR_incident_reopen_cycles_immutable' AND is_disabled=0)
              AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.incident_reopen_cycles')
                AND name IN('PK_incident_reopen_cycles','UQ_incident_reopen_work','UQ_incident_reopen_closure','UQ_incident_reopen_closure_audit','UQ_incident_reopen_audit') AND is_unique=1 AND is_disabled=0)=5
              AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.incident_reopen_cycles')
                AND name IN('CK_incident_reopen_time','CK_incident_reopen_identity') AND is_disabled=0 AND is_not_trusted=0)=2
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    private static void RequireTransaction(HotelOpsDbContext db)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Incident cycle requires its owning serializable transaction.");
    }

    // Missing storage is tolerated only for incidents that have never been reopened.
    internal static async Task<Boundary?> ReadAsync(HotelOpsDbContext db, long incident, CancellationToken ct, long? eventId = null)
    {
        RequireTransaction(db);
        var latestEvent = await db.IncidentEvents.AsNoTracking().Where(e => e.IncidentId == incident && e.EventType == "REOPENED"
            && (eventId == null || e.EventId == eventId))
            .OrderByDescending(e => e.EventId).FirstOrDefaultAsync(ct);
        if (!await InstalledAsync(db, ct))
        {
            if (latestEvent != null) throw new InternalRequestReadException(503, "INCIDENT_CYCLE_STORAGE_UNAVAILABLE");
            return null;
        }
        var row = await db.Database.SqlQuery<Boundary>($"""
            SELECT TOP(1) reopen_event_id AS EventId,incident_id AS IncidentId,previous_work_id AS PreviousWorkId,
              closure_event_id AS ClosureEventId,closure_audit_id AS ClosureAuditId,reopen_audit_id AS AuditId,
              recurrence_discovered_at AS DiscoveredAt,previous_closed_at AS ClosedAt,reopened_at AS ReopenedAt,
              previous_response_deadline_at AS PreviousResponseDeadlineAt,previous_resolution_deadline_at AS PreviousResolutionDeadlineAt
            FROM dbo.incident_reopen_cycles WITH(HOLDLOCK) WHERE incident_id={incident}
              AND ({eventId} IS NULL OR reopen_event_id={eventId}) ORDER BY reopen_event_id DESC
            """).SingleOrDefaultAsync(ct);
        if (row == null && latestEvent == null) return null;
        if (row == null || latestEvent == null || row.EventId != latestEvent.EventId
            || latestEvent.FromStatus != "CLOSED" || latestEvent.ToStatus != "OPEN" || latestEvent.OccurredAt != row.ReopenedAt
            || latestEvent.Note?.Trim().Length is not >= 10 || row.DiscoveredAt < row.ClosedAt || row.DiscoveredAt > row.ReopenedAt)
            throw new InternalRequestReadException(409, "INCIDENT_CYCLE_PROVENANCE_INVALID");
        var closure = await MaintenanceClosureSource.ReadHistoricalAsync(db, row.ClosureAuditId, ct);
        if (closure == null || closure.WorkId != row.PreviousWorkId || closure.IncidentId != incident || closure.At != row.ClosedAt
            || !await db.IncidentEvents.AsNoTracking().AnyAsync(e => e.EventId == row.ClosureEventId && e.IncidentId == incident
                && e.EventType == "STATUS_CHANGED" && e.FromStatus == "RESOLVED" && e.ToStatus == "CLOSED"
                && e.ActorId == closure.ActorId && e.OccurredAt == closure.At && e.Note == closure.Note, ct))
            throw new InternalRequestReadException(409, "INCIDENT_CYCLE_CLOSURE_INVALID");
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == row.AuditId, ct);
        if (audit == null || audit.EntityType != "incidents" || audit.EntityId != incident.ToString(CultureInfo.InvariantCulture)
            || audit.Action != "UPDATE" || audit.BranchId != closure.BranchId || audit.ActorUserId != latestEvent.ActorId
            || audit.OccurredAt != latestEvent.OccurredAt || !MatchesAudit(audit, row, latestEvent.Note!))
            throw new InternalRequestReadException(409, "INCIDENT_CYCLE_AUDIT_INVALID");
        return row;
    }

    private static bool MatchesAudit(AuditLog audit, Boundary row, string reason)
    {
        try
        {
            using var before = JsonDocument.Parse(audit.OldValues ?? "null");
            using var after = JsonDocument.Parse(audit.NewValues ?? "null");
            var b = before.RootElement; var a = after.RootElement;
            return b.GetProperty("status").GetString() == "CLOSED" && a.GetProperty("status").GetString() == "OPEN"
                && a.GetProperty("operation").GetString() == "REOPEN" && a.GetProperty("event_id").GetInt64() == row.EventId
                && a.GetProperty("recurrence_discovered_at").GetDateTime() == row.DiscoveredAt
                && a.GetProperty("reason").GetString() == reason
                && b.GetProperty("closure_audit_id").GetInt64() == row.ClosureAuditId
                && b.GetProperty("closed_at").GetDateTime() == row.ClosedAt
                && b.GetProperty("response_deadline_at").GetDateTime() == row.PreviousResponseDeadlineAt
                && b.GetProperty("resolution_deadline_at").GetDateTime() == row.PreviousResolutionDeadlineAt;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return false; }
    }

    internal static async Task AppendAsync(HotelOpsDbContext db, IncidentReopenSource.Closure closure,
        long eventId, long auditId, DateTime recurrence, DateTime reopenedAt, CancellationToken ct)
    {
        RequireTransaction(db);
        if (!await InstalledAsync(db, ct)) throw new InternalRequestReadException(503, "INCIDENT_CYCLE_STORAGE_UNAVAILABLE");
        if (recurrence.Kind != DateTimeKind.Utc || recurrence < closure.ClosedAtUtc || recurrence > reopenedAt
            || reopenedAt < closure.EvaluatedAtUtc || closure.PreviousSla.UnavailableReason != null
            || closure.PreviousSla.ResponseDeadlineAt == null || closure.PreviousSla.ResolutionDeadlineAt == null)
            throw new InternalRequestReadException(400, "INCIDENT_RECURRENCE_TIME_INVALID");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.incident_reopen_cycles(reopen_event_id,incident_id,previous_work_id,closure_event_id,closure_audit_id,reopen_audit_id,
              recurrence_discovered_at,previous_closed_at,reopened_at,previous_response_deadline_at,previous_resolution_deadline_at)
            VALUES({eventId},{closure.IncidentId},{closure.WorkId},{closure.EventId},{closure.AuditId},{auditId},
              {recurrence},{closure.ClosedAtUtc},{reopenedAt},{closure.PreviousSla.ResponseDeadlineAt},{closure.PreviousSla.ResolutionDeadlineAt})
            """, ct);
        if ((await ReadAsync(db, closure.IncidentId, ct))?.EventId != eventId)
            throw new InternalRequestReadException(409, "INCIDENT_CYCLE_PROVENANCE_INVALID");
    }
}
