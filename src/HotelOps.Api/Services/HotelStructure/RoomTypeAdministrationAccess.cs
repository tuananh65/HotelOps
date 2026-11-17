using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // UC142 and its detail/actions require Administrator on the same effective
    // master-data grant path. A role in another branch cannot lend actor eligibility.
    private async Task<PermissionEvaluation> RoomTypeAdministrationAsync(long? branch, CancellationToken ct)
    {
        var permission = branch is { } id
            ? await permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE", id, ct)
            : await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE", ct);
        if (!permission.IsGranted) return permission;
        if (!current.TryGetCurrentUser(out var actor) || actor.IsRestricted)
            return new(PermissionDecision.Denied);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var admitted = await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId == actor.UserId && role.IsActive && role.Role.IsActive
                && role.Role.RoleCode == "SYS_ADMIN" && role.AssignedAt <= now
                && (role.ExpiresAt == null || role.ExpiresAt > now)
                && grant.Permission.PermissionCode == "MASTER_DATA_MANAGE"
                && (role.BranchId == null && grant.DataScope == "ALL"
                    || branch != null && role.BranchId == branch
                        && (grant.DataScope == "ALL" || grant.DataScope == "BRANCH")
                        && role.Branch != null && role.Branch.IsActive
                        && db.UserBranchAccesses.Any(a => a.UserId == actor.UserId
                            && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now))
            select role.UserRoleId).AnyAsync(ct);
        return new(admitted ? PermissionDecision.Granted : PermissionDecision.Denied);
    }

    private async Task<PermissionEvaluation> RoomTypeListAccessAsync(long? branch, CancellationToken ct)
    {
        var global = await RoomTypeAdministrationAsync(branch, ct);
        if (branch != null || global.IsGranted || global.Decision == PermissionDecision.Unavailable) return global;
        foreach (var id in await db.Branches.AsNoTracking().Where(b => b.IsActive).Select(b => b.BranchId).ToArrayAsync(ct))
        {
            var scoped = await RoomTypeAdministrationAsync(id, ct);
            if (scoped.IsGranted || scoped.Decision == PermissionDecision.Unavailable) return scoped;
        }
        return new(PermissionDecision.Denied);
    }
}
