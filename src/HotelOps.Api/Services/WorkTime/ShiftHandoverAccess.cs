using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

internal static class ShiftHandoverAccess
{
    private static readonly string[] Actors = ["HK_SUPERVISOR", "HK_STAFF", "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER"];
    internal static async Task<bool> GrantAsync(HotelOpsDbContext db,long user,long branch,string permission,bool own,DateTime now,CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==user&&u.DeletedAt==null&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE",ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user&&a.BranchId==branch&&a.RevokedAt==null&&a.GrantedAt<=now&&a.Branch.IsActive,ct)) return false;
        return (await new EfPermissionDataSource(db).ReadPathsAsync(user,permission,now,ct)).Any(p=>PermissionGrantRules.IsEligible(p,now)
            && (PermissionGrantRules.EffectiveScope(p)=="GLOBAL" || p.BranchId==branch
                && (PermissionGrantRules.EffectiveScope(p)=="BRANCH" || own&&PermissionGrantRules.EffectiveScope(p)=="OWN")));
    }
    internal static Task<bool> RoleAsync(HotelOpsDbContext db,long user,long branch,DateTime now,CancellationToken ct,string[]? roles=null)
    {
        roles ??= Actors;
        return db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==user&&r.IsActive&&r.Role.IsActive&&roles.Contains(r.Role.RoleCode)
            &&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now)&&(r.BranchId==null||r.BranchId==branch),ct);
    }
    internal static async Task<bool> FeatureAsync(HotelOpsDbContext db,long user,long branch,string permission,bool own,DateTime now,CancellationToken ct)
        => await RoleAsync(db,user,branch,now,ct)&&await GrantAsync(db,user,branch,permission,own,now,ct);
    internal static async Task<bool> HeadAsync(HotelOpsDbContext db,long user,long branch,long department,DateTime now,CancellationToken ct)
    {
        if(!await RoleAsync(db,user,branch,now,ct,["DEPT_MANAGER"])||!await FeatureAsync(db,user,branch,"SHIFT_HANDOVER_VIEW",false,now,ct)) return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.department_management_access m WITH(HOLDLOCK)
            JOIN dbo.departments d WITH(HOLDLOCK) ON d.department_id=m.department_id AND d.branch_id=m.branch_id
            WHERE m.user_id={user} AND m.branch_id={branch} AND m.department_id={department}
              AND m.granted_at<={now} AND m.revoked_at IS NULL AND d.is_active=1
            """).AnyAsync(ct);
    }
    internal static async Task<bool> ReadAsync(HotelOpsDbContext db,long user,ShiftHandoverRow row,DateTime now,CancellationToken ct)
        => await FeatureAsync(db,user,row.BranchId,"SHIFT_HANDOVER_VIEW",user==row.SenderId||user==row.ReceiverId,now,ct)
            && (user==row.SenderId||user==row.ReceiverId||await HeadAsync(db,user,row.BranchId,row.DepartmentId,now,ct));
}
