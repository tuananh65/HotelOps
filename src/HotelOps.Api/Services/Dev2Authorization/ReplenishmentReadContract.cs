using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Replenishments;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static class ReplenishmentReadContract
{
    private const string Policy = "UC101 HTTPS current unrestricted SQL session; active STOREKEEPER or OPERATIONS_MANAGER; independent REPLENISHMENT_VIEW on actual warehouse branch; explicit branch-scoped list/count before pagination; descendant provenance and independent MR_VIEW for linked material detail; Serializable disclosure; no raw audit";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-052", "A004A-053", "A004A-055" }.Select(id => new Dev2ActionContract(
        id, "DEV3_142A_REPLENISHMENT_READ_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        async (a, ct) => [new("REPLENISHMENT_VIEW", new(Dev2PermissionTargetKind.Branch, await Branch(a, ct)))],
        Dev2PermissionCombination.All, Validate, (_, _) => false, null, Read, (_, _, _) => Task.FromResult(false), RequiresReadTransaction: true));
    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id>0 ? id : throw new Dev2AccessDenied(404);
    private static ReplenishmentPage Page(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("page") as ReplenishmentPage ?? throw new Dev2AccessDenied(400);
    private static async Task<long> Branch(Dev2BoundAction a, CancellationToken ct) => a.Identity.ActionName == "List"
        ? Page(a).BranchId is > 0 and var branch ? branch : throw new Dev2AccessDenied(400)
        : await Db(a).MaterialReplenishments.AsNoTracking()
        .Where(r => r.ReplenishmentId == Id(a)).Select(r => (long?)r.Warehouse.BranchId).SingleOrDefaultAsync(ct) ?? throw new Dev2AccessDenied(404);
    private static async Task<bool> Validate(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.ControllerType != typeof(ReplenishmentsController) || a.Identity.HttpMethod != "GET"
            || !(a.Identity.DecisionId == "A004A-052" && a.Identity.ActionName == "List"
                || a.Identity.DecisionId == "A004A-053" && a.Identity.ActionName == "Get" || a.Identity.DecisionId == "A004A-055" && a.Identity.ActionName == "Receipts")) return false;
        var db=Db(a);
        if (phase==Dev2AuthorizationPhase.BeforeDisclosure && db.Database.CurrentTransaction==null) return false;
        await using var admission=db.Database.CurrentTransaction==null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct) : null;
        var users=await new UserEligibilityLock(db).LockAsync([a.Actor.UserId],ct);
        if (!users.TryGetValue(a.Actor.UserId,out var user) || user is not { DeletedAt:null,AccountStatus:"ACTIVE",EmploymentStatus:"ACTIVE" }) return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User,out var ticket,out var sid) || ticket==null || ticket.IsRestricted
            || ticket.UserId!=a.Actor.UserId || sid!=a.Actor.SessionId) return false;
        var session=await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket,ct);
        if (!session.IsValid || session.Session?.SessionId!=sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var branch=await Branch(a,ct);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==a.Actor.UserId && r.IsActive && r.Role.IsActive
            && (r.Role.RoleCode=="STOREKEEPER" || r.Role.RoleCode=="OPERATIONS_MANAGER")
            && (r.BranchId==null || r.BranchId==branch) && r.AssignedAt<=now && (r.ExpiresAt==null || r.ExpiresAt>now)
            && db.UserBranchAccesses.Any(b=>b.UserId==a.Actor.UserId && b.BranchId==branch && b.Branch.IsActive && b.GrantedAt<=now && b.RevokedAt==null),ct)) return false;
        if(a.Identity.ActionName=="List")
            return Page(a).WarehouseId==null || await db.Warehouses.AsNoTracking().AnyAsync(w=>w.WarehouseId==Page(a).WarehouseId
                && w.IsActive && w.BranchId==branch && w.Branch.IsActive,ct);
        var root=await db.MaterialReplenishments.AsNoTracking().SingleAsync(r=>r.ReplenishmentId==Id(a),ct);
        if (!await db.Warehouses.AnyAsync(w=>w.WarehouseId==root.WarehouseId && w.IsActive && w.BranchId==branch,ct)) return false;
        if (await db.MaterialReplenishmentLines.AnyAsync(l=>l.ReplenishmentId==root.ReplenishmentId && (l.WarehouseId!=root.WarehouseId
            || l.SourceMaterialRequestLine!=null && l.SourceMaterialRequestLine.Request.BranchId!=branch),ct)
            || await db.MaterialReplenishmentReceiptLines.AnyAsync(l=>l.ReplenishmentLine.ReplenishmentId==root.ReplenishmentId
                && (l.StockDocumentLine.Document.DestinationWarehouseId!=root.WarehouseId || l.StockDocumentLine.Document.DocumentType!="RECEIPT"),ct)) return false;
        var linked=await db.MaterialReplenishmentLines.AsNoTracking().Where(l=>l.ReplenishmentId==root.ReplenishmentId && l.SourceMaterialRequestLine!=null)
            .Select(l=>l.SourceMaterialRequestLine!.RequestId).Distinct().ToArrayAsync(ct);
        var scope=await MaterialRequestReadAccess.ReadAsync(db,a.Actor.UserId,branch,now,ct);
        foreach(var id in linked)
            if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r=>r.RequestId==id,ct)
                || !await MaterialReadProvenance.ValidAsync(db,id,branch,ct)) return false;
        return true;
    }
    private static async Task<IActionResult> Read(Dev2BoundAction a,CancellationToken ct)
    {
        if(Db(a).Database.CurrentTransaction==null) throw new Dev2AccessDenied(503);
        try
        {
            var service=a.Http.RequestServices.GetRequiredService<ReplenishmentService>();
            if(a.Identity.ActionName=="List")
            {
                var branch=await Branch(a,ct);
                var scoped=Db(a).MaterialReplenishments.AsNoTracking().Where(r=>r.Warehouse.BranchId==branch
                    && r.Warehouse.IsActive && r.Warehouse.Branch.IsActive
                    && !r.MaterialReplenishmentLines.Any(l=>l.WarehouseId!=r.WarehouseId
                        || l.SourceMaterialRequestLine!=null && l.SourceMaterialRequestLine.Request.BranchId!=branch));
                return new OkObjectResult(await ReplenishmentService.ListAsync(scoped,Page(a),ct));
            }
            object? result=a.Identity.ActionName=="Get" ? await service.Get(Id(a),ct) : await service.Receipts(Id(a),ct);
            return result==null ? new NotFoundResult() : new OkObjectResult(result);
        }
        catch(ValidationException) { return new ConflictObjectResult(new ProblemDetails { Status=409,Title="Replenishment reconciliation required" }); }
    }
}
