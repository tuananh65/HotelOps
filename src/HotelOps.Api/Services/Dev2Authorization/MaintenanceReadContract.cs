using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dev2Authorization;

// Scoped list only; detail and mutations retain independent admission gates.
internal static class MaintenanceReadContract
{
    internal const string Reference = "DEV3_142A_MAINTENANCE_LIST_READ_V1";
    private const string Policy = "Explicit branch; current HTTPS SQL session; same-role MAINTENANCE_VIEW and active branch access; GBR165 Lead scoped branch, Staff assigned work only; unique coherent incident or preventive-run source; serializable read and final scope revalidation; no mutation or detail admission";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-035" }.Select(id =>
        new Dev2ActionContract(id, Reference, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
            async (a, ct) => [new("MAINTENANCE_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new("maintenance_list", await BranchAsync(a, ct))))],
            Dev2PermissionCombination.All, ValidateAsync, (_, _) => false, (a, _) => new Resolver(a), ReadAsync,
            (_, _, _) => Task.FromResult(false), RequiresReadTransaction: true));

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static MaintenanceQueryRequest Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("request") as MaintenanceQueryRequest
        ?? throw new Dev2AccessDenied(403);
    private static Task<long> BranchAsync(Dev2BoundAction a, CancellationToken ct) =>
        Task.FromResult(Input(a).BranchId is > 0 and var branch ? branch : throw new Dev2AccessDenied(400));
    private static readonly object ScopeKey = new();
    private sealed class Resolver(Dev2BoundAction a) : HotelOps.Api.Security.Authorization.IPermissionResourceResolver
    {
        public async Task<HotelOps.Api.Security.Authorization.VerifiedPermissionResource?> ResolveAsync(string permission,
            HotelOps.Api.Security.Authorization.PermissionResource resource, long user, CancellationToken ct)
        {
            var branch = await BranchAsync(a, ct);
            return permission == "MAINTENANCE_VIEW" && user == a.Actor.UserId && resource.ResourceType == "maintenance_list"
                && resource.ResourceId == branch ? new(branch, true) : null;
        }
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (a.Identity.ControllerType != typeof(MaintenanceWorksController) || a.Identity.HttpMethod != "GET"
            || a.Identity.DecisionId != "A004A-035" || a.Identity.ActionName != "GetWorks" || !a.Http.Request.IsHttps) return false;
        var db = Db(a);
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && db.Database.CurrentTransaction == null) return false;
        // The admission guard first calls us before installing its read transaction.
        // Session validation always requires a caller-owned locking transaction.
        await using var admission = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        await new UserEligibilityLock(db).LockAsync([a.Actor.UserId], ct);
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null
            || sid != a.Actor.SessionId || ticket.UserId != a.Actor.UserId || ticket.IsRestricted) return false;
        var valid = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var now = DateTime.UtcNow; var branch = await BranchAsync(a, ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(b => b.UserId == a.Actor.UserId && b.BranchId == branch
            && b.RevokedAt == null && b.GrantedAt <= now && b.Branch.IsActive, ct)) return false;
        var scope = await MaintenanceReadAccess.ReadAsync(db, a.Actor.UserId, branch, ct);
        if (!scope.Lead && !scope.Staff) return false;
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && a.Http.Items[ScopeKey] is MaintenanceReadAccess.Scope disclosed
            && (disclosed.Lead && !scope.Lead || disclosed.Staff && !scope.Lead && !scope.Staff)) return false;
        return true;
    }

    private static async Task<IActionResult> ReadAsync(Dev2BoundAction a, CancellationToken ct)
    {
        if (Db(a).Database.CurrentTransaction == null) throw new Dev2AccessDenied(503);
        var db = Db(a);
        var scope = await MaintenanceReadAccess.ReadAsync(db, a.Actor.UserId, await BranchAsync(a, ct), ct);
        var query = scope.Apply(db.MaintenanceWorks.AsNoTracking());
        a.Http.Items[ScopeKey] = scope;
        try { return new OkObjectResult(await MaintenanceWorkService.ListAsync(query, Input(a), ct)); }
        catch (ValidationException) { return new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = "Invalid maintenance filters" }); }
    }
}
