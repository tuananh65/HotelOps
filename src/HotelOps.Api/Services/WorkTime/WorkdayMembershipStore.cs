using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed record WorkdayDepartment(long DepartmentId, string Code, string Name);

/// <summary>Explicit employment membership, never department management authority or User.DepartmentId.</summary>
internal static class WorkdayMembershipStore
{
    internal static IQueryable<User> Employees(HotelOpsDbContext db, long branch, long? department, DateTime now)
        => db.Users.FromSqlInterpolated($"""
            SELECT u.* FROM dbo.users u WITH(HOLDLOCK)
            WHERE u.deleted_at IS NULL AND u.account_status='ACTIVE' AND u.employment_status='ACTIVE'
              AND EXISTS(SELECT 1 FROM dbo.employee_department_memberships m WITH(HOLDLOCK)
                JOIN dbo.departments d WITH(HOLDLOCK) ON d.department_id=m.department_id AND d.branch_id=m.branch_id
                JOIN dbo.branches b WITH(HOLDLOCK) ON b.branch_id=m.branch_id
                WHERE m.user_id=u.user_id AND m.branch_id={branch} AND m.department_id={department}
                  AND m.granted_at<={now} AND m.revoked_at IS NULL AND d.is_active=1 AND b.is_active=1)
              AND EXISTS(SELECT 1 FROM dbo.user_branch_access a WITH(HOLDLOCK)
                WHERE a.user_id=u.user_id AND a.branch_id={branch} AND a.granted_at<={now} AND a.revoked_at IS NULL)
            """).AsNoTracking();

    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => InstalledCoreAsync(db, ct);

    private static async Task<bool> InstalledCoreAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.UnscheduledWorkday.V1')
              AND OBJECT_ID('dbo.employee_department_memberships','U') IS NOT NULL
              AND OBJECT_ID('dbo.unscheduled_workday_notifications','U') IS NOT NULL
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN
                (OBJECT_ID('dbo.employee_department_memberships'),OBJECT_ID('dbo.unscheduled_workday_notifications'))
                AND is_disabled=0 AND is_not_trusted=0)=6
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static Task<WorkdayDepartment[]> ReadAsync(HotelOpsDbContext db, long user, long branch,
        DateTime now, CancellationToken ct)
        => db.Database.SqlQuery<WorkdayDepartment>($"""
            SELECT d.department_id AS DepartmentId,d.department_code AS Code,d.department_name AS Name
            FROM dbo.employee_department_memberships m WITH(HOLDLOCK)
            JOIN dbo.departments d WITH(HOLDLOCK) ON d.department_id=m.department_id AND d.branch_id=m.branch_id
            JOIN dbo.branches b WITH(HOLDLOCK) ON b.branch_id=m.branch_id
            WHERE m.user_id={user} AND m.branch_id={branch} AND m.granted_at<={now}
              AND m.revoked_at IS NULL AND d.is_active=1 AND b.is_active=1
            ORDER BY d.department_code,d.department_id
            """).ToArrayAsync(ct);

    internal static long Select(IReadOnlyList<WorkdayDepartment> memberships, long? selected)
    {
        if (memberships.Count == 0) throw new WorkTimeContractException("TIME_MEMBERSHIP_REQUIRED");
        if (selected is null)
        {
            if (memberships.Count != 1) throw new WorkTimeContractException("TIME_DEPARTMENT_SELECTION_REQUIRED");
            return memberships[0].DepartmentId;
        }
        if (selected <= 0 || !memberships.Any(x => x.DepartmentId == selected))
            throw new WorkTimeContractException("TIME_SCOPE_DENIED");
        return selected.Value;
    }
}
