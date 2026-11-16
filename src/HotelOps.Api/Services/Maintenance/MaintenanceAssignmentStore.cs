using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed class MaintenanceAssignmentReceipt
{
    public long AuditId { get; set; }
    public long WorkId { get; set; }
    public long IncidentId { get; set; }
    public long BranchId { get; set; }
    public long ActorId { get; set; }
    public long? PreviousAssigneeId { get; set; }
    public long AssigneeId { get; set; }
    public DateTime OccurredAt { get; set; }
}

internal static class MaintenanceAssignmentStore
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.MaintenanceAssignment.V1' AND CONVERT(nvarchar(10),value)='1')
              AND COL_LENGTH('dbo.maintenance_assignment_receipts','incident_version')=8
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_maintenance_assignment_immutable') AND is_disabled=0)
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.maintenance_assignment_receipts') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=7
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.maintenance_assignment_receipts') AND name='UQ_maintenance_assignment_version' AND is_unique=1 AND is_disabled=0)
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static async Task WriteAsync(HotelOpsDbContext db, MaintenanceAssignmentReceipt r, string tag, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !await InstalledAsync(db, ct)) throw new InvalidOperationException("Assignment receipt storage unavailable.");
        var version = Convert.FromHexString(tag.Trim('"'));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO dbo.maintenance_assignment_receipts(audit_id,work_id,incident_id,branch_id,actor_id,previous_assignee_id,assignee_id,occurred_at,incident_version)
            VALUES({r.AuditId},{r.WorkId},{r.IncidentId},{r.BranchId},{r.ActorId},{r.PreviousAssigneeId},{r.AssigneeId},{r.OccurredAt},{version})
            """, ct);
    }

    internal static async Task<MaintenanceAssignmentReceipt?> ReadAsync(HotelOpsDbContext db, long audit, CancellationToken ct)
    {
        if (!await InstalledAsync(db, ct)) return null;
        return await db.Database.SqlQuery<MaintenanceAssignmentReceipt>($"""
            SELECT r.audit_id AS AuditId,r.work_id AS WorkId,r.incident_id AS IncidentId,r.branch_id AS BranchId,r.actor_id AS ActorId,
              r.previous_assignee_id AS PreviousAssigneeId,r.assignee_id AS AssigneeId,r.occurred_at AS OccurredAt
            FROM dbo.maintenance_assignment_receipts r JOIN dbo.audit_logs a ON a.audit_id=r.audit_id
            WHERE r.audit_id={audit} AND a.entity_type='maintenance_works' AND TRY_CONVERT(bigint,a.entity_id)=r.work_id
              AND a.branch_id=r.branch_id AND a.actor_user_id=r.actor_id AND a.occurred_at=r.occurred_at
            """).SingleOrDefaultAsync(ct);
    }

    internal static async Task<bool> CanReadReceiptAsync(HotelOpsDbContext db, long user, MaintenanceAssignmentReceipt r, CancellationToken ct)
    {
        if (r.PreviousAssigneeId != user) return false;
        var access = await MaintenanceReadAccess.ReadAsync(db, user, r.BranchId, ct);
        return AllowsReceipt(user,r,access);
    }

    internal static bool AllowsReceipt(long user,MaintenanceAssignmentReceipt r,MaintenanceReadAccess.Scope access)
        => user>0 && r.PreviousAssigneeId==user && access.User==user && access.Branch==r.BranchId && (access.Staff || access.Lead);
}
