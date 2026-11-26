using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Security;

public sealed class Access(HotelOpsCompactDbContext db,Actor actor,TimeProvider clock)
{
    public async Task<bool> Allows(string code,long? branchId=null,bool owns=false,long? departmentId=null,CancellationToken ct=default)
    {
        var now=clock.GetUtcNow().UtcDateTime; var userId=actor.Id;
        if(!await db.Users.AnyAsync(u=>u.UserId==userId&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null,ct))return false;
        if(!await db.UserSessions.AnyAsync(s=>s.SessionId==actor.SessionId&&s.UserId==userId&&s.RevokedAt==null&&s.ExpiresAt>now,ct))return false;
        var paths=await (from ur in db.UserRoles.AsNoTracking()
            join role in db.Roles on ur.RoleId equals role.RoleId
            join rp in db.RolePermissions on role.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.PermissionId
            where ur.UserId==userId && ur.AssignedAt<=now && ur.RevokedAt==null && (ur.ExpiresAt==null || ur.ExpiresAt>now)
                && role.IsActive && p.IsActive && p.Code==code
                && !(role.Code=="HOUSEKEEPING_SUPERVISOR" && p.Code=="ROOM_VIEW")
            select new {ur.BranchId,rp.DataScope}).ToArrayAsync(ct);
        if(branchId!=null && !await db.Branches.AnyAsync(b=>b.BranchId==branchId&&b.IsActive,ct))return false;
        if(paths.Any(p=>p.DataScope=="ALL" && p.BranchId==null))return true;
        if(branchId==null)return false;
        if(!await db.Branches.AsNoTracking().AnyAsync(b=>b.BranchId==branchId && b.IsActive,ct))return false;
        var membership=await db.Memberships.AsNoTracking().AnyAsync(m=>m.UserId==userId && m.BranchId==branchId
            && m.Kind=="BRANCH_ACCESS" && m.ValidFrom<=now && (m.ValidTo==null || m.ValidTo>now),ct);
        if(!membership)return false;
        // Preserve the existing authorization policy: legacy DEPARTMENT never grants implicitly.
        return paths.Any(p=>p.BranchId==branchId && (p.DataScope is "ALL" or "BRANCH" || p.DataScope=="OWN" && owns));
    }
    public async Task Require(string code,long? branchId=null,bool owns=false,CancellationToken ct=default)
    {
        if(!await Allows(code,branchId,owns,ct:ct))throw new ApiException(403,"PERMISSION_DENIED");
        if(actor.IsBusinessCommand&&branchId is long branch)
            await ShiftCommandAccess.Require(db,actor.Id,branch,clock.GetUtcNow().UtcDateTime,ct,code,owns);
    }
    public Task<bool> HasRole(string code,long branchId,CancellationToken ct){var now=clock.GetUtcNow().UtcDateTime;
        return (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.RoleId
            where ur.UserId==actor.Id&&ur.BranchId==branchId&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&r.IsActive&&r.Code==code select ur.UserRoleId).AnyAsync(ct);}
    public async Task RequireRolePermission(string roleCode,string permission,long branchId,CancellationToken ct){
        await Require(permission,branchId,ct:ct);var now=clock.GetUtcNow().UtcDateTime;
        var valid=await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId join grant in db.RolePermissions on role.RoleId equals grant.RoleId join p in db.Permissions on grant.PermissionId equals p.PermissionId
            where ur.UserId==actor.Id&&ur.BranchId==branchId&&ur.RevokedAt==null&&ur.AssignedAt<=now&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&role.IsActive&&role.Code==roleCode&&p.Code==permission&&p.IsActive&&(grant.DataScope=="BRANCH"||grant.DataScope=="ALL") select ur.UserRoleId).AnyAsync(ct);
        Output.Require(valid,"ROLE_PERMISSION_PATH_REQUIRED",403);
    }
    public async Task EligibleFor(long userId,long branchId,string permission,CancellationToken ct){
        await Eligible(userId,branchId,ct);var now=clock.GetUtcNow().UtcDateTime;
        var valid=await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId join grant in db.RolePermissions on role.RoleId equals grant.RoleId join p in db.Permissions on grant.PermissionId equals p.PermissionId
            where ur.UserId==userId&&ur.RevokedAt==null&&ur.AssignedAt<=now&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&role.IsActive&&p.IsActive&&p.Code==permission
            &&((ur.BranchId==null&&grant.DataScope=="ALL")||(ur.BranchId==branchId&&(grant.DataScope=="ALL"||grant.DataScope=="BRANCH"||grant.DataScope=="OWN"))) select ur.UserRoleId).AnyAsync(ct);
        Output.Require(valid,"ASSIGNEE_EXECUTION_PERMISSION_REQUIRED",403);
    }
    public async Task Eligible(long userId,long branchId,CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        var valid=await db.Users.AnyAsync(u=>u.UserId==userId && u.AccountStatus=="ACTIVE" && u.EmploymentStatus=="ACTIVE" && u.DeletedAt==null,ct)
            && await db.Memberships.AnyAsync(m=>m.UserId==userId && m.BranchId==branchId && m.Kind=="BRANCH_ACCESS"
                && m.ValidFrom<=now && (m.ValidTo==null || m.ValidTo>now),ct);
        Output.Require(valid,"ASSIGNEE_NOT_ACTIVE_IN_BRANCH");
    }
}
