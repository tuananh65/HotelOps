using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Users;

public sealed record EmployeeMembershipPeriod(long HistoryId,long DepartmentId,string Name,DateTime GrantedAt,
    DateTime RevokedAt,long RecordedBy,string Provenance,long RestoredBy,DateTime RestoredAt);

internal static class EmployeeMembershipHistory
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.EmployeeMembershipHistory.V1' AND CONVERT(nvarchar(10),value)='1')
              AND ISNULL(COL_LENGTH('dbo.employee_department_membership_history','source_version'),-1)=8
              AND ISNULL(COL_LENGTH('dbo.employee_department_membership_history','provenance'),-1)=500
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.employee_department_membership_history') AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
              AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.employee_department_membership_history') AND is_disabled=0 AND is_not_trusted=0)=2
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.employee_department_membership_history') AND name='UQ_employee_membership_history_version' AND is_unique=1 AND is_disabled=0)
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;

    internal static Task<EmployeeMembershipPeriod[]> ReadAsync(HotelOpsDbContext db,long user,long branch,CancellationToken ct)=>
        db.Database.SqlQuery<EmployeeMembershipPeriod>($"""
            SELECT h.history_id AS HistoryId,h.department_id AS DepartmentId,d.department_name AS Name,
              h.granted_at AS GrantedAt,h.revoked_at AS RevokedAt,h.recorded_by AS RecordedBy,h.provenance AS Provenance,
              h.restored_by AS RestoredBy,h.restored_at AS RestoredAt
            FROM dbo.employee_department_membership_history h JOIN dbo.departments d
              ON d.department_id=h.department_id AND d.branch_id=h.branch_id
            WHERE h.user_id={user} AND h.branch_id={branch}
            """).OrderByDescending(h=>h.HistoryId).ToArrayAsync(ct);
}
