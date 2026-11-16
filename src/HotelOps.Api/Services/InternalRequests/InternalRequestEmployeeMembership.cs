using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

// Employment membership is independent of the explicit Department Head authority mapping.
internal static class InternalRequestEmployeeMembership
{
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => WorkdayMembershipStore.InstalledAsync(db, ct);

    internal static IQueryable<User> Members(HotelOpsDbContext db, long branch, long? department, DateTime now)
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
}
