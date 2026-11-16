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

// Scoped list/detail; material usage and mutations retain independent admission gates.
internal static class MaintenanceReadContract
{
    internal const string Reference = "DEV3_142A_MAINTENANCE_READ_V3";
    private const string Policy = "List explicit branch, detail SQL-derived branch; current HTTPS SQL session; same-role MAINTENANCE_VIEW and active branch access; GBR165 Lead scoped branch, Staff assigned work only; unique coherent incident or preventive-run source; serializable read and final scope revalidation; detail usage requires independent MR_VIEW and INVENTORY_VIEW, coherent issuance lineage and final disclosure revalidation; material availability requires independent INVENTORY_VIEW and MR_VIEW with request-scope filtering before counts/paging, catalog lock before user/source and final grant revalidation; no mutation admission";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-034", "A004A-035", "A004A-036" }.Select(id =>
        new Dev2ActionContract(id, Reference, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
            async (a, ct) => [new("MAINTENANCE_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new(IsList(a) ? "maintenance_list" : "maintenance_work", IsList(a) ? await BranchAsync(a, ct) : Id(a))))],
            Dev2PermissionCombination.All, ValidateAsync, (_, _) => false, (a, _) => new Resolver(a), ReadAsync,
            (_, _, _) => Task.FromResult(false), RequiresReadTransaction: true));

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static MaintenanceQueryRequest Input(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("request") as MaintenanceQueryRequest
        ?? throw new Dev2AccessDenied(403);
    private static bool IsAvailability(Dev2BoundAction a) => a.Identity.DecisionId == "A004A-034";
    private static readonly object AvailabilityScopeKey = new();
    private static bool IsList(Dev2BoundAction a) => a.Identity.DecisionId == "A004A-035";
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static async Task<long> BranchAsync(Dev2BoundAction a, CancellationToken ct)
    {
        if (IsList(a)) return Input(a).BranchId is > 0 and var branch ? branch : throw new Dev2AccessDenied(400);
        return await Db(a).MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == Id(a))
            .Select(w => w.Incident != null ? (long?)w.Incident.BranchId : w.PreventiveMaintenanceRuns
                .Select(r => r.Schedule.Asset != null ? (long?)r.Schedule.Asset.BranchId
                    : r.Schedule.Room != null ? (long?)r.Schedule.Room.BranchId
                    : r.Schedule.Area != null ? (long?)r.Schedule.Area.BranchId : null).FirstOrDefault())
            .SingleOrDefaultAsync(ct) ?? throw new Dev2AccessDenied(404);
    }
    private static readonly object ScopeKey = new();
    private static readonly object UsageKey = new();
    private static readonly object ResumeKey = new();
    private sealed class Resolver(Dev2BoundAction a) : HotelOps.Api.Security.Authorization.IPermissionResourceResolver
    {
        public async Task<HotelOps.Api.Security.Authorization.VerifiedPermissionResource?> ResolveAsync(string permission,
            HotelOps.Api.Security.Authorization.PermissionResource resource, long user, CancellationToken ct)
        {
            var branch = await BranchAsync(a, ct);
            if (permission != "MAINTENANCE_VIEW" || user != a.Actor.UserId) return null;
            if (IsList(a)) return resource.ResourceType == "maintenance_list" && resource.ResourceId == branch ? new(branch, true) : null;
            if (resource.ResourceType != "maintenance_work" || resource.ResourceId != Id(a)) return null;
            var assigned = await Db(a).MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == Id(a))
                .Select(w => w.AssigneeId).SingleOrDefaultAsync(ct);
            return new(branch, assigned == user);
        }
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (a.Identity.ControllerType != typeof(MaintenanceWorksController) || a.Identity.HttpMethod != "GET"
            || !(a.Identity.DecisionId == "A004A-035" && a.Identity.ActionName == "GetWorks"
                || a.Identity.DecisionId == "A004A-036" && a.Identity.ActionName == "GetWork"
                || IsAvailability(a) && a.Identity.ActionName == "MaterialAvailability") || !a.Http.Request.IsHttps) return false;
        var db = Db(a);
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && db.Database.CurrentTransaction == null) return false;
        // The admission guard first calls us before installing its read transaction.
        // Session validation always requires a caller-owned locking transaction.
        await using var admission = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        if (IsAvailability(a)) await a.Http.RequestServices.GetRequiredService<HotelOps.Api.Services.Inventory.IInventoryIssueCapacity>().AcquireCatalog(ct);
        await new UserEligibilityLock(db).LockAsync([a.Actor.UserId], ct);
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null
            || sid != a.Actor.SessionId || ticket.UserId != a.Actor.UserId || ticket.IsRestricted) return false;
        var valid = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var now = DateTime.UtcNow; var branch = await BranchAsync(a, ct);
        if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(b => b.UserId == a.Actor.UserId && b.BranchId == branch
            && b.RevokedAt == null && b.GrantedAt <= now && b.Branch.IsActive, ct)) return false;
        if (IsAvailability(a))
        {
            var materialScope = await MaintenanceMaterialAccess.ScopeAsync(db, a.Actor.UserId, branch, ct);
            if (phase == Dev2AuthorizationPhase.BeforeDisclosure && a.Http.Items[AvailabilityScopeKey] is HotelOps.Api.Services.MaterialRequests.MaterialRequestReadAccess.Scope prior
                && (prior.Branch && !materialScope.Branch || prior.Own && !materialScope.Own && !materialScope.Branch)) return false;
        }
        var scope = await MaintenanceReadAccess.ReadAsync(db, a.Actor.UserId, branch, ct);
        if (!scope.Lead && !scope.Staff) return false;
        if (!IsList(a) && !await scope.Apply(db.MaintenanceWorks.AsNoTracking()).AnyAsync(w => w.MaintenanceWorkId == Id(a), ct))
            throw new Dev2AccessDenied(404);
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && a.Http.Items.ContainsKey(ResumeKey)
            && !await MaintenanceExecutionAccess.AllowsAsync(db, a.Actor.UserId, branch, Id(a), ct)) return false;
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && a.Http.Items[ScopeKey] is MaintenanceReadAccess.Scope disclosed
            && (disclosed.Branch != scope.Branch || disclosed.Lead && !scope.Lead || disclosed.Staff && !scope.Lead && !scope.Staff)) return false;
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && !IsList(a)
            && a.Http.Items[UsageKey] is MaintenanceUsageRead.Result { Available: true } shown)
        {
            var fresh = await MaintenanceUsageRead.ReadAsync(db, a.Actor.UserId, branch, Id(a), ct);
            if (!fresh.Available || !fresh.Items.SequenceEqual(shown.Items)) return false;
        }
        return true;
    }

    private static async Task<IActionResult> ReadAsync(Dev2BoundAction a, CancellationToken ct)
    {
        if (Db(a).Database.CurrentTransaction == null) throw new Dev2AccessDenied(503);
        var db = Db(a);
        var scope = await MaintenanceReadAccess.ReadAsync(db, a.Actor.UserId, await BranchAsync(a, ct), ct);
        var query = scope.Apply(db.MaintenanceWorks.AsNoTracking());
        a.Http.Items[ScopeKey] = scope;
        try
        {
            if (IsAvailability(a))
            {
                a.Http.Items[AvailabilityScopeKey] = await MaintenanceMaterialAccess.ScopeAsync(db, a.Actor.UserId, scope.Branch, ct);
                if (a.Http.RequestServices.GetRequiredService<IMaintenanceWorkService>() is not MaintenanceWorkService service) throw new Dev2AccessDenied(503);
                var page = a.Arguments.GetValueOrDefault("page") is int p ? p : 1;
                var size = a.Arguments.GetValueOrDefault("pageSize") is int s ? s : 20;
                var available = await service.ScopedMaterialAvailabilityAsync(a.Actor.UserId, scope.Branch, Id(a), page, size, ct);
                return available == null ? new NotFoundResult() : new OkObjectResult(available);
            }
            if (IsList(a)) return new OkObjectResult(await MaintenanceWorkService.ListAsync(query, Input(a), ct));
            var detail = await MaintenanceWorkService.DetailAsync(query, Id(a), ct);
            if (detail == null) return new NotFoundResult();
            detail.ExecutionETag = await MaintenanceExecutionVersion.TagAsync(db, Id(a), ct);
            detail.CanStart = detail.Status == "PENDING" && detail.ActualStartAt == null && detail.ExecutionETag != null
                && await MaintenanceExecutionAccess.AllowsAsync(db, a.Actor.UserId, scope.Branch, Id(a), ct);
            detail.CanResume = detail.Status == "ON_HOLD" && detail.ExecutionETag != null
                && await MaintenanceExecutionAccess.AllowsAsync(db, a.Actor.UserId, scope.Branch, Id(a), ct);
            detail.CanHold = detail.Status == "IN_PROGRESS" && detail.ExecutionETag != null
                && await MaintenanceExecutionAccess.AllowsAsync(db, a.Actor.UserId, scope.Branch, Id(a), ct)
                && await db.WorkIntervals.AsNoTracking().AnyAsync(i => i.UserId == a.Actor.UserId && i.BranchId == scope.Branch
                    && i.WorkItemKind == "MAINTENANCE" && i.WorkItemId == Id(a) && i.ClosedAt == null, ct);
            detail.CanSubmit = detail.CanHold;
            if (detail.CanStart || detail.CanResume || detail.CanHold) a.Http.Items[ResumeKey] = true;
            var usage = await MaintenanceUsageRead.ReadAsync(db, a.Actor.UserId, scope.Branch, Id(a), ct);
            a.Http.Items[UsageKey] = usage;
            detail.MaterialUsageAvailable = usage.Available;
            if (usage.Available)
                foreach (var log in detail.MaintenanceWorkLogs)
                    log.MaterialUsages = usage.Items.Where(u => u.MaintenanceWorkLogId == log.LogId).ToArray();
            return new OkObjectResult(detail);
        }
        catch (ValidationException) { return new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = "Invalid maintenance filters" }); }
    }
}
