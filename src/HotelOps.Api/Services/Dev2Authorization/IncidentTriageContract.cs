using System.Data;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static class IncidentTriageContract
{
    private const string Policy = "UC052 initial Triage only: HTTPS current unrestricted SQL session; current branch Maintenance Lead with INCIDENT_TRIAGE and independent INCIDENT_VIEW; exact request/root binding; Serializable transaction, native If-Match, current configured SLA target version, classified Pending Triage source; discovery-based defaults with reasoned overrides and atomic event/audit; no Cancel or reassignment admission";
    internal static IEnumerable<Dev2ActionContract> Contracts => [new("A004A-020", "DEV3_142A_INCIDENT_TRIAGE_V1",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, ct) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>([
            new("INCIDENT_TRIAGE", new(Dev2PermissionTargetKind.Resource, Resource: new("incident", Id(a)))),
            new("INCIDENT_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("incident", Id(a))))]),
        Dev2PermissionCombination.All, Validate,
        (a, call) => call.Controller == "Incidents" && call.Action == "TriageIncident" && call.RootId == Id(a)
            && ReferenceEquals(call.Input, Input(a)), (a, _) => new Resolver(a), null, Validate, RequiresActionTransaction: true)];

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static TriageIncidentRequest Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("request") as TriageIncidentRequest ?? throw new Dev2AccessDenied(403);
    private static Task<long?> Branch(Dev2BoundAction a, CancellationToken ct) => Db(a).Incidents.AsNoTracking()
        .Where(i => i.IncidentId == Id(a)).Select(i => (long?)i.BranchId).SingleOrDefaultAsync(ct);
    private sealed class Resolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permission, PermissionResource resource, long user, CancellationToken ct)
        {
            if (user != a.Actor.UserId || permission is not ("INCIDENT_TRIAGE" or "INCIDENT_VIEW")
                || resource.ResourceType != "incident" || resource.ResourceId != Id(a)) return null;
            var branch = await Branch(a, ct);
            var own = permission == "INCIDENT_VIEW" && await Db(a).Incidents.AsNoTracking()
                .AnyAsync(i => i.IncidentId == Id(a) && i.ReportedBy == user, ct);
            return branch == null ? null : new(branch.Value, own);
        }
    }
    private static async Task<bool> Validate(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.DecisionId != "A004A-020" || a.Identity.ControllerType != typeof(IncidentsController)
            || a.Identity.HttpMethod != "PATCH" || a.Identity.ActionName != "TriageIncident" || Input(a).ActorUserId != a.Actor.UserId) return false;
        var db = Db(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        await new UserEligibilityLock(db).LockAsync([a.Actor.UserId], ct);
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await Dev2SessionValidation.ValidateAsync(db, ticket, a.Http.RequestServices.GetRequiredService<TimeProvider>(), ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        if (!await IncidentTriageStore.InstalledAsync(db, ct)) throw new Dev2AccessDenied(503);
        var branch = await Branch(a, ct);
        return branch != null && await IncidentTriageAccess.AllowsAsync(db, a.Actor.UserId, branch.Value, Id(a), ct);
    }
}
