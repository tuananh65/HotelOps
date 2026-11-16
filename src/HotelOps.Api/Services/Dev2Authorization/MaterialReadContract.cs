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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static class MaterialReadContract
{
    private static readonly object DisclosedScopeKey = new();
    private const string Policy = "Material list/detail/availability/fulfillment receipts: HTTPS current unrestricted SQL session; independent MR_VIEW; "
        + "SQL-derived branch/requester scope and descendant provenance; availability additionally requires INVENTORY_VIEW on actual warehouse branch, UC096 actor and current workflow/reviewer authority; "
        + "serializable through final disclosure; no reservation, mutation or raw audit authority";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-037", "A004A-038", "A004A-040", "A004A-041", "A004A-042" }.Select(id =>
        new Dev2ActionContract(id, "DEV3_142A_MATERIAL_READ_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
            RequirementsAsync,
            Dev2PermissionCombination.All, ValidateAsync, (_, _) => false, (a, _) => new Resolver(a),
            ReadAsync, (_, _, _) => Task.FromResult(false), RequiresReadTransaction: true));

    private static async Task<IReadOnlyList<Dev2PermissionRequirement>> RequirementsAsync(Dev2BoundAction a, CancellationToken ct)
    {
        var branch = await BranchAsync(a, ct);
        var requirements = new List<Dev2PermissionRequirement>
        {
            new("MR_VIEW", new(Dev2PermissionTargetKind.Resource,
                Resource: new(a.Identity.ActionName == "List" ? "material_list" : "material_request",
                    a.Identity.ActionName == "List" ? branch : Id(a))))
        };
        if (a.Identity.ActionName == "Availability") requirements.Add(new("INVENTORY_VIEW", new(Dev2PermissionTargetKind.Branch, branch)));
        return requirements;
    }

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static MaterialPage Page(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("page") as MaterialPage ?? throw new Dev2AccessDenied(400);
    private static async Task<long> BranchAsync(Dev2BoundAction a, CancellationToken ct) => a.Identity.ActionName == "List"
        ? Page(a).BranchId is > 0 and var branch ? branch : throw new Dev2AccessDenied(400)
        : await Db(a).MaterialRequests.AsNoTracking().Where(r => r.RequestId == Id(a)).Select(r => (long?)r.BranchId).SingleOrDefaultAsync(ct)
            ?? throw new Dev2AccessDenied(404);

    private sealed class Resolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource, long user, CancellationToken ct)
        {
            if (permissionCode != "MR_VIEW" || user != a.Actor.UserId) return null;
            var branch = await BranchAsync(a, ct);
            if (a.Identity.ActionName == "List")
                return resource.ResourceType == "material_list" && resource.ResourceId == branch ? new(branch, true) : null;
            if (resource.ResourceType != "material_request" || resource.ResourceId != Id(a)) return null;
            var requester = await Db(a).MaterialRequests.AsNoTracking().Where(r => r.RequestId == Id(a)).Select(r => r.RequesterId).SingleAsync(ct);
            return new(branch, requester == user);
        }
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.ControllerType != typeof(MaterialRequestsController) || a.Identity.HttpMethod != "GET"
            || !(a.Identity.DecisionId == "A004A-037" && a.Identity.ActionName == "List"
                || a.Identity.DecisionId == "A004A-038" && a.Identity.ActionName == "Get"
                || a.Identity.DecisionId == "A004A-040" && a.Identity.ActionName == "Availability"
                || a.Identity.DecisionId == "A004A-041" && a.Identity.ActionName == "Fulfillments"
                || a.Identity.DecisionId == "A004A-042" && a.Identity.ActionName == "Fulfillment")) return false;
        var db = Db(a);
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && db.Database.CurrentTransaction == null) return false;
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        // The pending-review authority check locks requester and approver. Acquire
        // both in canonical order before any actor/source locks to avoid inversion.
        var requester = a.Identity.ActionName == "Availability"
            ? await db.MaterialRequests.AsNoTracking().Where(r => r.RequestId == Id(a)).Select(r => (long?)r.RequesterId).SingleOrDefaultAsync(ct)
            : null;
        var users = await new UserEligibilityLock(db).LockAsync(requester.HasValue ? [a.Actor.UserId, requester.Value] : [a.Actor.UserId], ct);
        if (!users.TryGetValue(a.Actor.UserId, out var actor) || actor == null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, a.Actor.UserId, await BranchAsync(a, ct), now, ct);
        if (!scope.Branch && !scope.Own) return false;
        // A branch grant can expire while an OWN grant remains valid. Do not
        // publish a page selected under the broader, now-expired scope.
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure
            && (a.Http.Items[DisclosedScopeKey] is not MaterialRequestReadAccess.Scope selectedScope || selectedScope != scope)) return false;
        if (a.Identity.ActionName != "List")
        {
            if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == Id(a), ct)) throw new Dev2AccessDenied(404);
            if (!await MaterialReadProvenance.ValidAsync(db, Id(a), scope.BranchId, ct)) throw new Dev2AccessDenied(404);
            if (a.Identity.ActionName == "Fulfillment" && (a.Arguments.GetValueOrDefault("fulfillmentId") is not long fulfillmentId
                || fulfillmentId <= 0 || !await db.MaterialFulfillments.AsNoTracking().AnyAsync(f => f.FulfillmentId == fulfillmentId && f.RequestId == Id(a), ct)))
                throw new Dev2AccessDenied(404);
            if (a.Identity.ActionName == "Availability" && !await db.MaterialRequests.AsNoTracking().AnyAsync(r => r.RequestId == Id(a)
                && r.SourceWarehouse != null && r.SourceWarehouse.IsActive && r.SourceWarehouse.BranchId == r.BranchId, ct))
                throw new Dev2AccessDenied(404);
            if (a.Identity.ActionName == "Availability"
                && !await MaterialAvailabilityAccess.CanReadAsync(db, Id(a), a.Actor.UserId, now, ct))
                throw new Dev2AccessDenied(403);
        }
        return true;
    }

    private static async Task<IActionResult> ReadAsync(Dev2BoundAction a, CancellationToken ct)
    {
        var db = Db(a);
        if (db.Database.CurrentTransaction == null) throw new Dev2AccessDenied(503);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, a.Actor.UserId, await BranchAsync(a, ct), now, ct);
        a.Http.Items[DisclosedScopeKey] = scope;
        try
        {
            if (a.Identity.ActionName == "List") return new OkObjectResult(await MaterialRequestService.ListAsync(scope.Apply(db.MaterialRequests.AsNoTracking()), Page(a), ct));
            if (a.Identity.ActionName is "Fulfillments" or "Fulfillment")
            {
                var service = a.Http.RequestServices.GetRequiredService<MaterialRequestService>();
                object? receipt = a.Identity.ActionName == "Fulfillments" ? await service.Fulfillments(Id(a), ct)
                    : await service.Fulfillment(Id(a), (long)a.Arguments["fulfillmentId"]!, ct);
                return receipt == null ? new NotFoundResult() : new OkObjectResult(receipt);
            }
            if (a.Identity.ActionName == "Availability")
            {
                var availability = await a.Http.RequestServices.GetRequiredService<MaterialRequestService>().Availability(Id(a), ct);
                return availability == null ? new NotFoundResult() : new OkObjectResult(availability);
            }
            var result = await a.Http.RequestServices.GetRequiredService<MaterialRequestService>().Get(Id(a), ct);
            return result == null ? new NotFoundResult() : new OkObjectResult(result);
        }
        catch (ValidationException) { return new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = "Invalid material filters" }); }
    }
}
