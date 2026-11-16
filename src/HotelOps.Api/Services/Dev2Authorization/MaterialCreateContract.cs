using System.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Services.Dev2Authorization;

// UC091 creation only. Other material mutations retain their separate admission.
// The client actor is checked for consistency; session/principal and SQL grant paths
// establish authority. The outer action owns commit through response validation.
internal static class MaterialCreateContract
{
    private const string ResourceType = "material_request_creation";
    private static readonly object ApproverOptionsKey = new();
    private static readonly object CatalogKey = new();
    private const string Policy = "UC091 create: HTTPS current unrestricted SQL session; eligible actor; "
        + "active branch access; approved actor role with MR_CREATE and independent MR_VIEW in current scope; "
        + "requester always principal; exact bound input; Serializable action transaction through disclosure; "
        + "approved mapped approver and independent source-link authorization; lifecycle schema required; scoped current mapped approver lookup; "
        + "branch catalog selection without stock/price disclosure; independent incident read scope rechecked before catalog disclosure";

    internal static IEnumerable<Dev2ActionContract> Contracts =>
    [new("A004A-043", "DEV3_142A_MATERIAL_CREATE_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, _) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>(
            [new("MR_CREATE", new(Dev2PermissionTargetKind.Resource, Resource: new(ResourceType, Input(a).BranchId))),
             new("MR_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new(ResourceType, Input(a).BranchId)))]),
        Dev2PermissionCombination.All, ValidateAsync,
        (a, invocation) => invocation.Controller == "MaterialRequests" && invocation.Action == "Create"
            && invocation.RootId == null && ReferenceEquals(invocation.Input, Input(a)),
        (a, _) => new CreationResolver(a), null,
        (a, phase, ct) => ValidateAsync(a, phase, ct), RequiresActionTransaction: true),
     new("MATERIAL-CREATE-APPROVERS", "DEV3_142A_MATERIAL_CREATE_APPROVERS_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, _) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>(
            [new("MR_CREATE", new(Dev2PermissionTargetKind.Resource, Resource: new(ResourceType, Input(a).BranchId))),
             new("MR_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new(ResourceType, Input(a).BranchId)))]),
        Dev2PermissionCombination.All, ValidateAsync, (_, _) => false, (a, _) => new CreationResolver(a),
        ReadApproversAsync, (_, _, _) => Task.FromResult(false), RequiresReadTransaction: true),
     new("MATERIAL-CREATE-CATALOG", "DEV3_142A_MATERIAL_CREATE_CATALOG_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        (a, _) => Task.FromResult<IReadOnlyList<Dev2PermissionRequirement>>(
            [new("MR_CREATE", new(Dev2PermissionTargetKind.Resource, Resource: new(ResourceType, Input(a).BranchId))),
             new("MR_VIEW", new(Dev2PermissionTargetKind.Resource, Resource: new(ResourceType, Input(a).BranchId)))]),
        Dev2PermissionCombination.All, ValidateAsync, (_, _) => false, (a, _) => new CreationResolver(a),
        ReadCatalogAsync, (_, _, _) => Task.FromResult(false), RequiresReadTransaction: true)];

    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static MaterialRequestWrite Input(Dev2BoundAction a)
    {
        if (a.Identity.DecisionId is not ("MATERIAL-CREATE-APPROVERS" or "MATERIAL-CREATE-CATALOG"))
            return a.Arguments.GetValueOrDefault("input") as MaterialRequestWrite ?? throw new Dev2AccessDenied(403);
        if (a.Arguments.GetValueOrDefault("branchId") is not long branch || branch <= 0) throw new Dev2AccessDenied(400);
        var warehouse = a.Arguments.GetValueOrDefault("sourceWarehouseId") as long?;
        if (warehouse is <= 0) throw new Dev2AccessDenied(400);
        return new(a.Actor.UserId, branch, 0, warehouse, "", null, null, null, null, null, []);
    }

    private static async Task<IActionResult> ReadApproversAsync(Dev2BoundAction a, CancellationToken ct)
    {
        var input = Input(a);
        try
        {
            var result = await MaterialApprovalAuthority.CandidatesAsync(Db(a), a.Actor.UserId, input.BranchId, input.SourceWarehouseId, ct);
            a.Http.Items[ApproverOptionsKey] = result;
            return new OkObjectResult(result);
        }
        catch (ValidationException e) { return new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = "Approver routing unavailable", Detail = e.Message }); }
    }

    private static async Task<IActionResult> ReadCatalogAsync(Dev2BoundAction a, CancellationToken ct)
    {
        var catalog = await MaterialCreationCatalog.ReadAsync(Db(a), a.Actor.UserId, Input(a).BranchId, ct);
        a.Http.Items[CatalogKey] = catalog;
        return new OkObjectResult(catalog);
    }

    private sealed class CreationResolver(Dev2BoundAction a) : IPermissionResourceResolver
    {
        public async Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode, PermissionResource resource,
            long currentUserId, CancellationToken ct)
        {
            // A creation context is the actual active branch + authenticated requester,
            // not a fabricated persisted material ID or client-supplied owner claim.
            var input = Input(a);
            if (permissionCode is not ("MR_CREATE" or "MR_VIEW") || resource.ResourceType != ResourceType
                || resource.ResourceId != input.BranchId || currentUserId != a.Actor.UserId
                || input.ActorUserId != currentUserId) return null;
            return await Db(a).Branches.AsNoTracking().AnyAsync(b => b.BranchId == input.BranchId && b.IsActive, ct)
                ? new(input.BranchId, true) : null;
        }
    }

    private static async Task<bool> ValidateAsync(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        var lookup = a.Identity.HttpMethod == "GET" && (a.Identity.DecisionId == "MATERIAL-CREATE-APPROVERS" && a.Identity.ActionName == "Approvers"
            || a.Identity.DecisionId == "MATERIAL-CREATE-CATALOG" && a.Identity.ActionName == "CreationCatalog");
        if (a.Identity.ControllerType != typeof(MaterialRequestsController) || !a.Http.Request.IsHttps
            || !(lookup || a.Identity.ActionName == "Create" && a.Identity.DecisionId == "A004A-043" && a.Identity.HttpMethod == "POST")) return false;
        var input = Input(a);
        if (input.ActorUserId != a.Actor.UserId || input.BranchId <= 0 || input.Lines == null
            || input.AssignedApproverUserId is <= 0) return false;
        var db = Db(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        await using var admission = db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        // Same deterministic user-lock order as the writer, before session/catalog/source locks.
        long[] ids = input.Submit && input.AssignedApproverUserId is long approver
            ? [a.Actor.UserId, approver] : [a.Actor.UserId];
        var users = await new UserEligibilityLock(db).LockAsync(ids, ct);
        if (!users.TryGetValue(a.Actor.UserId, out var actor) || actor is null || actor.DeletedAt != null
            || actor.AccountStatus != "ACTIVE" || actor.EmploymentStatus != "ACTIVE") return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null
            || sid != a.Actor.SessionId || ticket.UserId != a.Actor.UserId || ticket.IsRestricted) return false;
        var session = await Dev2SessionValidation.ValidateAsync(db, ticket,
            a.Http.RequestServices.GetRequiredService<TimeProvider>(), ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!(await MaterialCreationAccess.BranchesAsync(db, a.Actor.UserId, now, ct)).Any(b => b.BranchId == input.BranchId)) return false;
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.MaterialLifecycle.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.material_requests','row_version')=8
              AND OBJECT_ID('dbo.material_request_approval_cycles','U') IS NOT NULL
              AND OBJECT_ID('dbo.material_request_approval_line_snapshots','U') IS NOT NULL
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) throw new Dev2AccessDenied(503);
        if (input.IncidentId is long incident)
        {
            var scope = await IncidentReadAccess.ReadAsync(db, a.Actor.UserId, input.BranchId, ct);
            if (!await scope.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == incident, ct)) return false;
        }
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && lookup)
        {
            if (a.Http.Items[CatalogKey] is MaterialCreationCatalog.Catalog catalog && catalog.Incidents.Length > 0)
            {
                var incidentScope = await IncidentReadAccess.ReadAsync(db, a.Actor.UserId, input.BranchId, ct);
                var incidentIds = catalog.Incidents.Select(i => i.Id).ToArray();
                if (await incidentScope.Apply(db.Incidents.AsNoTracking()).CountAsync(i => incidentIds.Contains(i.IncidentId), ct) != incidentIds.Length) return false;
            }
            if (a.Http.Items[ApproverOptionsKey] is MaterialApprovalAuthority.Options selected)
            {
                MaterialApprovalAuthority.Options current;
                try { current = await MaterialApprovalAuthority.CandidatesAsync(db, a.Actor.UserId, input.BranchId, input.SourceWarehouseId, ct); }
                catch (ValidationException) { return false; }
                if (current.DepartmentId != selected.DepartmentId || current.RequiredPermission != selected.RequiredPermission
                    || !current.Items.SequenceEqual(selected.Items)) return false;
            }
        }
        if (phase == Dev2AuthorizationPhase.BeforeDisclosure && !lookup)
        {
            var created = db.MaterialRequests.Local.Where(r => r.RequesterId == a.Actor.UserId && r.BranchId == input.BranchId).ToArray();
            if (created.Length != 1 || created[0].RequestId <= 0) return false;
            var scope = await MaterialRequestReadAccess.ReadAsync(db, a.Actor.UserId, input.BranchId, now, ct);
            if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == created[0].RequestId, ct)) return false;
            try
            {
                if (input.Submit)
                {
                    if (input.AssignedApproverUserId is not long selected) return false;
                    await MaterialApprovalAuthority.ValidateAsync(db, created[0], selected, ct);
                }
                await MaterialRequestCreation.RevalidateAsync(db, a.Actor.UserId, input, ct);
            }
            catch (ValidationException) { throw new Dev2AccessDenied(403); }
            catch (InternalRequestReadException e) { throw new Dev2AccessDenied(e.Status); }
        }
        return true;
    }
}
