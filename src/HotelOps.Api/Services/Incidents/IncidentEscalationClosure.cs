using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Incidents;

// Transaction participant, not HTTP or escalation creation authority.
internal static class IncidentEscalationClosure
{
    internal sealed class Closed
    {
        public long EscalationId { get; set; }
        public long OpenedEventId { get; set; }
        public DateTime OpenedAt { get; set; }
    }

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.IncidentEscalation.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.incident_escalations','reason')=8000
              AND COL_LENGTH('dbo.incident_escalations','requested_support')=8000
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.incident_escalations')
                AND name IN('FK_incident_escalation_source','FK_incident_escalation_open','FK_incident_escalation_close')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
              AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.incident_escalations')
                AND name IN('CK_incident_escalation_reason','CK_incident_escalation_support','CK_incident_escalation_state')
                AND is_disabled=0 AND is_not_trusted=0)=3
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.incident_escalations')
                AND name='UX_incident_escalation_open' AND is_unique=1 AND is_disabled=0 AND filter_definition=N'([state]=''OPEN'')')
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static async Task<Closed[]> CloseAsync(HotelOpsDbContext db, long incident, long branch,
        long actor, long closingEvent, DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || !await InstalledAsync(db, ct)) throw new Dev2AccessDenied(503);
        // Bind to the persisted transition in this source transaction, never just a client ID.
        if (!await db.IncidentEvents.AsNoTracking().AnyAsync(e => e.EventId == closingEvent && e.IncidentId == incident
            && e.ActorId == actor && e.OccurredAt == now && e.EventType == "STATUS_CHANGED"
            && e.FromStatus != e.ToStatus && (e.ToStatus == "CLOSED" || e.ToStatus == "CANCELLED")
            && e.Incident.BranchId == branch && e.Incident.Status == e.ToStatus, ct))
            throw new Dev2AccessDenied(409);
        // OUTPUT is materialized directly (no LINQ composition around this DML).
        return await db.Database.SqlQuery<Closed>($"""
            UPDATE dbo.incident_escalations WITH(UPDLOCK,HOLDLOCK)
            SET state='CLOSED',closed_at={now},closed_event_id={closingEvent}
            OUTPUT inserted.escalation_id AS EscalationId,inserted.opened_event_id AS OpenedEventId,inserted.opened_at AS OpenedAt
            WHERE incident_id={incident} AND state='OPEN';
            """).ToArrayAsync(ct);
    }
}
