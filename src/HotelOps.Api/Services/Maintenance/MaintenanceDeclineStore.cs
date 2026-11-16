using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceDeclineStore
{
    internal static async Task<IReadOnlyList<MaintenanceDeclineHistory>> ReadAsync(HotelOpsDbContext db, long work, CancellationToken ct)
        => await db.Database.SqlQuery<MaintenanceDeclineHistory>($"""
            SELECT audit_id AS AuditId, previous_assignee_id AS PreviousAssigneeId, reason AS Reason, occurred_at AS OccurredAt
            FROM dbo.maintenance_decline_events WHERE maintenance_work_id={work} ORDER BY occurred_at,audit_id
            """).ToArrayAsync(ct);

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.MaintenanceDecline.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.maintenance_decline_events','reason')=4000
              AND COL_LENGTH('dbo.maintenance_decline_events','assignment_version')=8
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.maintenance_decline_events')
                AND name='UQ_maintenance_decline_assignment' AND is_unique=1 AND is_disabled=0)
              AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.maintenance_decline_events')
                AND name IN('CK_maintenance_decline_actor','CK_maintenance_decline_reason') AND is_disabled=0 AND is_not_trusted=0)=2
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_maintenance_decline_immutable') AND is_disabled=0)
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.maintenance_decline_events')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=4
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static async Task WriteAsync(HotelOpsDbContext db, long audit, long work, long incident, long actor,
        string version, string reason, DateTime at, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !await InstalledAsync(db, ct))
            throw new InvalidOperationException("Installed decline storage and owning transaction required.");
        var bytes = Convert.FromHexString(version.Trim('"'));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.maintenance_decline_events(audit_id,maintenance_work_id,incident_id,previous_assignee_id,
              actor_id,assignment_version,reason,occurred_at)
            VALUES({audit},{work},{incident},{actor},{actor},{bytes},{reason},{at})
            """, ct);
    }
}

public sealed class MaintenanceDeclineHistory
{
    public long AuditId { get; set; }
    public long PreviousAssigneeId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime OccurredAt { get; set; }
}
