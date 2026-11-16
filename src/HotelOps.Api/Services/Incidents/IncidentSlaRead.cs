using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

// Read-only storage contract. Triage must record configured SLA targets and audit;
// never substitute a maintenance plan or calculate an unapproved duration here.
internal static class IncidentSlaRead
{
    internal static IncidentSlaDto Normalize(DateTime discovered, DateTime? response, DateTime? resolution)
        => response < discovered || resolution < discovered
            ? new(null, null, "INCIDENT_SLA_INVALID")
            : new(Utc(response), Utc(resolution), response == null || resolution == null ? "INCIDENT_SLA_NOT_RECORDED" : null);
    private static DateTime? Utc(DateTime? value) => value is {} date ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null;

    // Call only after the incident itself was authorized within the owning read transaction.
    internal static async Task<IncidentSlaDto> ReadAsync(HotelOpsDbContext db, long incident, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Owning read transaction required.");
        var installed = await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name=N'HotelOps.IncidentSla.V1' AND CONVERT(nvarchar(20),value)=N'1')
              AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.incidents')
                AND name IN('response_deadline_at','resolution_deadline_at') AND system_type_id=42 AND scale=7 AND is_nullable=1)=2
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct);
        if (installed != 1) return new(null, null, "INCIDENT_SLA_STORAGE_UNAVAILABLE");
        var row = await db.Database.SqlQuery<Row>($"""
            SELECT discovered_at AS DiscoveredAt, response_deadline_at AS ResponseDeadlineAt,
              resolution_deadline_at AS ResolutionDeadlineAt FROM dbo.incidents WHERE incident_id={incident}
            """).SingleAsync(ct);
        return Normalize(row.DiscoveredAt, row.ResponseDeadlineAt, row.ResolutionDeadlineAt);
    }

    internal static async Task<IncidentSlaDto> ForWorkAsync(HotelOpsDbContext db, long actor, long branch, long work, CancellationToken ct)
    {
        var incident = await db.MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == work)
            .Select(w => w.IncidentId).SingleAsync(ct);
        if (incident == null) return new(null, null, "NOT_APPLICABLE");
        var scope = await IncidentReadAccess.ReadAsync(db, actor, branch, ct, sourceRecord: true);
        if (!await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == incident, ct))
            return new(null, null, "INCIDENT_SLA_ACCESS_UNAVAILABLE");
        return await ReadAsync(db, incident.Value, ct);
    }
    private sealed class Row
    {
        public DateTime DiscoveredAt { get; set; }
        public DateTime? ResponseDeadlineAt { get; set; }
        public DateTime? ResolutionDeadlineAt { get; set; }
    }
}
