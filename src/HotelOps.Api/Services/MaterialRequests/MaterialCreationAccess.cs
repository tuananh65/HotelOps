using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

public sealed record MaterialCreationBranch(long BranchId, string Name);

public sealed class MaterialCreationAccess(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    private static readonly string[] CreatorRoles = ["SYS_ADMIN", "OPERATIONS_MANAGER", "HK_SUPERVISOR", "HK_STAFF",
        "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER"];

    // Creation owns its prospective request. OWN is applicable only in that
    // context; this list never grants access to another request or another user.
    internal static async Task<MaterialCreationBranch[]> BranchesAsync(HotelOpsDbContext db, long actor, DateTime now, CancellationToken ct)
    {
        var create = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == actor && CreatorRoles.Contains(assignment.Role.RoleCode) && grant.Permission.PermissionCode == "MR_CREATE"
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == actor && b.BranchId == assignment.BranchId && b.RevokedAt == null && b.GrantedAt <= now))).ToArrayAsync(ct);
        var view = await new EfPermissionDataSource(db).ReadPathsAsync(actor, "MR_VIEW", now, ct);
        var branches = await db.UserBranchAccesses.AsNoTracking().Where(b => b.UserId == actor && b.RevokedAt == null
            && b.GrantedAt <= now && b.Branch.IsActive).Select(b => new MaterialCreationBranch(b.BranchId, b.Branch.BranchName))
            .Distinct().OrderBy(b => b.Name).ThenBy(b => b.BranchId).ToArrayAsync(ct);
        static bool Allows(PermissionGrantPath p, long branch, DateTime at) => PermissionGrantRules.IsEligible(p, at)
            && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL" || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN");
        return branches.Where(b => create.Any(p => Allows(p, b.BranchId, now))
            && view.Any(p => p.PermissionCode == "MR_VIEW" && Allows(p, b.BranchId, now))).ToArray();
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    public async Task<MaterialCreationBranch[]> ReadAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var locked = await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        if (!locked.TryGetValue(ticket.UserId, out var actor) || actor == null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") throw new InternalRequestReadException(403, "MR_CREATE_REQUIRED");
        await SessionAsync(ticket, sid, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var result = await BranchesAsync(db, ticket.UserId, now, ct);
        await SessionAsync(ticket, sid, ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var confirmed = await BranchesAsync(db, ticket.UserId, now, ct);
        if (!result.SequenceEqual(confirmed)) throw new InternalRequestReadException(409, "MR_ACCESS_CHANGED");
        await tx.CommitAsync(ct);
        return result;
    }
}
