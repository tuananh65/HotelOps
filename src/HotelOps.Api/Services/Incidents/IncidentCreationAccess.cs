using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed record IncidentCreationBranch(long BranchId, string Name);
public sealed record IncidentRoomChoice(long Id, string Name, long? AreaId);
public sealed record IncidentAreaChoice(long Id, string Name);
public sealed record IncidentAssetChoice(long Id, string Name, string Code, long? RoomId, long? AreaId);
public sealed record IncidentLocationChoices(IncidentRoomChoice[] Rooms, IncidentAreaChoice[] Areas, IncidentAssetChoice[] Assets);

public sealed class IncidentCreationAccess(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    public async Task<IncidentLocationChoices> LocationsAsync(SessionReference ticket, long sid, long branchId, CancellationToken ct)
    {
        if (!(await ReadAsync(ticket, sid, ct)).Any(b => b.BranchId == branchId))
            throw new InternalRequestReadException(403, "INCIDENT_CREATE_REQUIRED");
        var rooms = await db.Rooms.AsNoTracking().Where(r => r.BranchId == branchId && r.IsActive
            && r.Floor.IsActive && r.Floor.BranchId == branchId
            && (r.AreaId == null || r.Area != null && r.Area.IsActive && r.Area.BranchId == branchId))
            .OrderBy(r => r.RoomNumber).Select(r => new IncidentRoomChoice(r.RoomId, r.RoomNumber, r.AreaId)).ToArrayAsync(ct);
        var areas = await db.Areas.AsNoTracking().Where(a => a.BranchId == branchId && a.IsActive)
            .OrderBy(a => a.AreaName).Select(a => new IncidentAreaChoice(a.AreaId, a.AreaName)).ToArrayAsync(ct);
        var assets = await db.Assets.AsNoTracking().Where(a => a.BranchId == branchId
            && (a.LifecycleStatus == "IN_USE" || a.LifecycleStatus == "UNDER_REPAIR"))
            .OrderBy(a => a.AssetName).ThenBy(a => a.AssetCode)
            .Select(a => new IncidentAssetChoice(a.AssetId, a.AssetName, a.AssetCode, a.CurrentRoomId, a.CurrentAreaId)).ToArrayAsync(ct);
        if (!(await ReadAsync(ticket, sid, ct)).Any(b => b.BranchId == branchId))
            throw new InternalRequestReadException(403, "INCIDENT_CREATE_REQUIRED");
        return new(rooms, areas, assets);
    }
    private static readonly string[] CreatorRoles = ["SYS_ADMIN", "OPERATIONS_MANAGER", "HK_SUPERVISOR", "HK_STAFF",
        "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER"];
    internal static bool IsCreatorRole(string? role) => role != null && CreatorRoles.Contains(role, StringComparer.Ordinal);

    // Creation owns its prospective request. OWN is applicable only in that
    // context; this list never grants access to another request or another user.
    internal static async Task<IncidentCreationBranch[]> BranchesAsync(HotelOpsDbContext db, long actor, DateTime now, CancellationToken ct)
    {
        var create = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == actor && CreatorRoles.Contains(assignment.Role.RoleCode) && grant.Permission.PermissionCode == "INCIDENT_CREATE"
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == actor && b.BranchId == assignment.BranchId && b.RevokedAt == null && b.GrantedAt <= now))).ToArrayAsync(ct);
        var branches = await db.UserBranchAccesses.AsNoTracking().Where(b => b.UserId == actor && b.RevokedAt == null
            && b.GrantedAt <= now && b.Branch.IsActive).Select(b => new { b.BranchId, Name = b.Branch.BranchName, b.IsPrimary })
            .OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Name).ToArrayAsync(ct);
        var result = new List<IncidentCreationBranch>();
        foreach (var branch in branches)
        {
            if (!create.Any(p => p.BranchId == branch.BranchId && PermissionGrantRules.IsEligible(p, now)
                && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN")) continue;
            var read = await IncidentReadAccess.ReadAsync(db, actor, branch.BranchId, ct, sourceRecord: true);
            if (read.Lead || read.Staff) result.Add(new(branch.BranchId, branch.Name));
        }
        return result.ToArray();
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    public async Task<IncidentCreationBranch[]> ReadAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var locked = await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        if (!locked.TryGetValue(ticket.UserId, out var actor) || actor == null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") throw new InternalRequestReadException(403, "INCIDENT_CREATE_REQUIRED");
        await SessionAsync(ticket, sid, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var result = await BranchesAsync(db, ticket.UserId, now, ct);
        await SessionAsync(ticket, sid, ct);
        now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var confirmed = await BranchesAsync(db, ticket.UserId, now, ct);
        if (!result.SequenceEqual(confirmed)) throw new InternalRequestReadException(409, "INCIDENT_ACCESS_CHANGED");
        await tx.CommitAsync(ct);
        return result;
    }
}
