using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dev2Authorization;

// Approved GBR111 department routing, current assignment and independent MR_VIEW.
// Material service retains version/state/quantity/reason/audit business checks.
internal static class MaterialApprovalContract
{
    private static readonly object FactsKey = new();
    private sealed record Facts(long Request, long Branch, long Requester, string Permission);
    private static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>
    {
        ["A004A-046"] = "Information", ["A004A-047"] = "Approve", ["A004A-048"] = "Reject", ["A004A-050"] = "Close"
    };
    private const string Policy = "HTTPS unrestricted current SQL session; active assigned independent DEPT_MANAGER; "
        + "exact requester-department L1 or issuing-warehouse-department FALLBACK mapping; independent MR_VIEW; "
        + "Serializable action through final disclosure; exact principal/input/root binding; existing version/state/audit guards";
    internal static IEnumerable<Dev2ActionContract> Contracts => Actions.Keys.Select(id => new Dev2ActionContract(
        id, "DEV3_142A_MATERIAL_APPROVAL_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, _) =>
        {
            var facts = CurrentFacts(a);
            return Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>(
                [new(facts.Permission, new(Dev2PermissionTargetKind.Branch, facts.Branch)),
                 new("MR_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("material_request", facts.Request)))]);
        }, Dev2PermissionCombination.All, ValidateAsync,
        (a, invocation) => invocation.Controller == "MaterialRequests" && invocation.Action == a.Identity.ActionName
            && invocation.RootId == Id(a) && ReferenceEquals(invocation.Input, Input(a)),
        (a, _) => new Resolver(a), null, ValidateAsync, RequiresActionTransaction: true));

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static object Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("input") ?? throw new Dev2AccessDenied(400);
    private static Facts CurrentFacts(Dev2BoundAction a) => a.Http.Items[FactsKey] as Facts ?? throw new Dev2AccessDenied(403);
    private sealed class Resolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource, long actor, CancellationToken ct)
        {
            var facts = CurrentFacts(a);
            if (permissionCode != "MR_VIEW" || actor != a.Actor.UserId || resource.ResourceType != "material_request" || resource.ResourceId != facts.Request) return null;
            var row = await Db(a).MaterialRequests.AsNoTracking().Where(r => r.RequestId == facts.Request)
                .Select(r => new { r.BranchId, r.RequesterId }).SingleOrDefaultAsync(ct);
            return row != null && row.BranchId == facts.Branch && row.RequesterId == facts.Requester
                ? new(row.BranchId, row.RequesterId == actor) : null;
        }
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.ControllerType != typeof(MaterialRequestsController) || a.Identity.HttpMethod != "POST"
            || !Actions.TryGetValue(a.Identity.DecisionId, out var action) || a.Identity.ActionName != action) return false;
        var claimed = Input(a) switch { MaterialApproval p when action == "Approve" => p.ActorUserId,
            MaterialCommand p when action != "Approve" => p.ActorUserId, _ => 0 };
        if (claimed != a.Actor.UserId) return false;
        var db = Db(a); var id = Id(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        var hint = await db.MaterialRequests.AsNoTracking().Where(r => r.RequestId == id).Select(r => new { r.RequesterId, r.BranchId }).SingleOrDefaultAsync(ct)
            ?? throw new Dev2AccessDenied(404);
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        var users = await new UserEligibilityLock(db).LockAsync([a.Actor.UserId, hint.RequesterId], ct);
        if (!users.TryGetValue(a.Actor.UserId, out var actor) || actor == null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var request = await db.MaterialRequests.AsNoTracking().SingleOrDefaultAsync(r => r.RequestId == id, ct);
        if (request == null || request.RequesterId != hint.RequesterId || request.BranchId != hint.BranchId) return false;
        var cycle = await db.MaterialRequestApprovalCycles.AsNoTracking().Where(c => c.RequestId == id)
            .OrderByDescending(c => c.RevisionNo).Select(c => new { c.AssignedApproverId, c.SubmittedBy }).FirstOrDefaultAsync(ct);
        if (cycle == null || cycle.AssignedApproverId != a.Actor.UserId || cycle.SubmittedBy == a.Actor.UserId || request.RequesterId == a.Actor.UserId) return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        try
        {
            await MaterialApprovalAuthority.ValidateAsync(db, request, a.Actor.UserId, ct);
            var route = await MaterialApprovalAuthority.ResolveAsync(db, request, now, ct);
            a.Http.Items[FactsKey] = new Facts(id, request.BranchId, request.RequesterId, route.Permission);
        }
        catch (ValidationException) { return false; }
        var scope = await MaterialRequestReadAccess.ReadAsync(db, a.Actor.UserId, request.BranchId, now, ct);
        if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == id, ct)) return false;
        return await MaterialReadProvenance.ValidAsync(db, id, request.BranchId, ct);
    }
}
