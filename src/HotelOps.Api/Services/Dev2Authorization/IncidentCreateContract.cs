using System.Data;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dev2Authorization;

// UC049 only: the action transaction owns persistence, evidence lifetime and disclosure.
internal static class IncidentCreateContract
{
    private const string Resource = "incident_creation";
    private const string Policy = "UC049 HTTPS current unrestricted SQL session; active approved operational actor with INCIDENT_CREATE and independent INCIDENT_VIEW; exact principal-owned branch creation context; current location consistency; serializable user-first transaction and final authority revalidation; scoped duplicate review; atomic incident, evidence, source links, audit and notification intent";
    private static readonly string[] Actors = ["SYS_ADMIN", "OPERATIONS_MANAGER", "HK_SUPERVISOR", "HK_STAFF",
        "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER", "RECEPTION"];

    internal static IEnumerable<Dev2ActionContract> Contracts =>
    [new("A004A-021", "DEV3_6CORE_INCIDENT_CREATE_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, _) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>([
            new("INCIDENT_CREATE", new(Dev2PermissionTargetKind.Resource, Resource: new(Resource, Input(a).BranchId))),
            new("INCIDENT_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new(Resource, Input(a).BranchId)))]),
        Dev2PermissionCombination.All, ValidateAsync,
        (a, call) => call.Controller == "Incidents" && call.Action == "CreateIncident" && call.RootId == null
            && ReferenceEquals(call.Input, Input(a)),
        (a, _) => new Resolver(a), null, ValidateAsync, RequiresActionTransaction: true)];

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static CreateIncidentRequest Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("request") as CreateIncidentRequest
        ?? throw new Dev2AccessDenied(403);

    private sealed class Resolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permission, PermissionResource resource, long user, CancellationToken ct)
        {
            var input = Input(a);
            if (permission is not ("INCIDENT_CREATE" or "INCIDENT_VIEW") || resource.ResourceType != Resource
                || resource.ResourceId != input.BranchId || user != a.Actor.UserId || input.ReportedBy != user) return null;
            return await Db(a).Branches.AsNoTracking().AnyAsync(b => b.BranchId == input.BranchId && b.IsActive, ct)
                ? new(input.BranchId, true) : null;
        }
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.ControllerType != typeof(IncidentsController)
            || a.Identity.DecisionId != "A004A-021" || a.Identity.ActionName != "CreateIncident" || a.Identity.HttpMethod != "POST") return false;
        var input = Input(a);
        if (input.ReportedBy != a.Actor.UserId || input.BranchId <= 0) return false;
        var db = Db(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        await using var admission = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        var users = await new UserEligibilityLock(db).LockAsync([a.Actor.UserId], ct);
        if (!users.TryGetValue(a.Actor.UserId, out var actor) || actor == null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await Dev2SessionValidation.ValidateAsync(db, ticket, a.Http.RequestServices.GetRequiredService<TimeProvider>(), ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var grants = await (from assignment in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on assignment.RoleId equals grant.RoleId
            where assignment.UserId == a.Actor.UserId && assignment.BranchId == input.BranchId
                && Actors.Contains(assignment.Role.RoleCode) && grant.Permission.PermissionCode == "INCIDENT_CREATE"
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, assignment.BranchId,
                assignment.IsActive, assignment.Role.IsActive, assignment.AssignedAt, assignment.ExpiresAt,
                assignment.Branch != null && assignment.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == a.Actor.UserId && b.BranchId == input.BranchId
                    && b.RevokedAt == null && b.GrantedAt <= now))).ToArrayAsync(ct);
        if (!grants.Any(g => PermissionGrantRules.IsEligible(g, now) && PermissionGrantRules.EffectiveScope(g) is "OWN" or "BRANCH")) return false;
        var read = await IncidentReadAccess.ReadAsync(db, a.Actor.UserId, input.BranchId, ct, sourceRecord: true);
        if (!read.Lead && !read.Staff) return false;
        if (input.RoomId is long room && !await db.Rooms.AsNoTracking().AnyAsync(r => r.RoomId == room && r.IsActive
            && r.BranchId == input.BranchId && r.Floor.IsActive && r.Floor.BranchId == input.BranchId
            && (r.AreaId == null || r.Area != null && r.Area.IsActive && r.Area.BranchId == input.BranchId)
            && (input.AreaId == null || r.AreaId == input.AreaId), ct)) return false;
        if (input.AreaId is long area && !await db.Areas.AsNoTracking().AnyAsync(r => r.AreaId == area && r.IsActive && r.BranchId == input.BranchId, ct)) return false;
        if (input.AssetId is long asset && !await db.Assets.AsNoTracking().AnyAsync(r => r.AssetId == asset
            && r.BranchId == input.BranchId && (r.LifecycleStatus == "IN_USE" || r.LifecycleStatus == "UNDER_REPAIR")
            && (input.RoomId == null || r.CurrentRoomId == input.RoomId)
            && (input.AreaId == null || r.CurrentAreaId == input.AreaId), ct)) return false;
        return true;
    }
}
