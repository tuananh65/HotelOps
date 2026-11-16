using System.Data;
using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.MaterialRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Dev2Authorization;

internal static class ReplenishmentWriteContract
{
    private const string Policy = "UC100/101 create/close: HTTPS unrestricted SQL session; active STOREKEEPER and current branch access; "
        + "independent REPLENISHMENT_CREATE/CLOSE plus REPLENISHMENT_VIEW, INVENTORY_VIEW for shortage creation; "
        + "independent MR_VIEW and same-warehouse material provenance; exact principal/input/root binding; "
        + "Serializable action through persistence/disclosure, existing coverage/concurrency/reconciliation/audit preserved";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-056", "A004A-057" }.Select(id => new Dev2ActionContract(
        id, "DEV3_142A_REPLENISHMENT_WRITE_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        async (a, ct) => {
            var branch = (await Warehouse(a, ct)).BranchId;
            var requirements = new List<Dev2PermissionRequirement> {
                new(IsCreate(a) ? "REPLENISHMENT_CREATE" : "REPLENISHMENT_CLOSE", new(Dev2PermissionTargetKind.Branch, branch)),
                new("REPLENISHMENT_VIEW", new(Dev2PermissionTargetKind.Branch, branch)) };
            if (IsCreate(a)) requirements.Add(new("INVENTORY_VIEW", new(Dev2PermissionTargetKind.Branch, branch)));
            return requirements;
        }, Dev2PermissionCombination.All, Validate,
        (a, call) => call.Controller == "Replenishments" && call.Action == (IsCreate(a) ? "Create" : "Close")
            && call.RootId == (IsCreate(a) ? null : Id(a)) && ReferenceEquals(call.Input, Input(a)),
        null, null, Validate, RequiresActionTransaction: true));

    private static bool IsCreate(Dev2BoundAction a) => a.Identity.DecisionId == "A004A-056";
    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static object Input(Dev2BoundAction a) => IsCreate(a)
        ? a.Arguments.GetValueOrDefault("input") as ReplenishmentCreate ?? throw new Dev2AccessDenied(400)
        : a.Arguments.GetValueOrDefault("input") as ReplenishmentClose ?? throw new Dev2AccessDenied(400);
    private sealed record WarehouseFact(long Id, long BranchId);
    private static async Task<WarehouseFact> Warehouse(Dev2BoundAction a, CancellationToken ct)
    {
        var id = IsCreate(a) ? ((ReplenishmentCreate)Input(a)).WarehouseId
            : await Db(a).MaterialReplenishments.AsNoTracking().Where(r => r.ReplenishmentId == Id(a)).Select(r => (long?)r.WarehouseId).SingleOrDefaultAsync(ct)
                ?? throw new Dev2AccessDenied(404);
        return await Db(a).Warehouses.AsNoTracking().Where(w => w.WarehouseId == id && w.IsActive && w.Branch.IsActive)
            .Select(w => new WarehouseFact(w.WarehouseId, w.BranchId)).SingleOrDefaultAsync(ct) ?? throw new Dev2AccessDenied(404);
    }

    private static async Task<bool> Validate(Dev2BoundAction a, Dev2AuthorizationPhase phase, CancellationToken ct)
    {
        if (!a.Http.Request.IsHttps || a.Identity.ControllerType != typeof(ReplenishmentsController) || a.Identity.HttpMethod != "POST"
            || !(IsCreate(a) && a.Identity.ActionName == "Create" || a.Identity.DecisionId == "A004A-057" && a.Identity.ActionName == "Close")) return false;
        var claimed = Input(a) is ReplenishmentCreate create ? create.ActorUserId : ((ReplenishmentClose)Input(a)).ActorUserId;
        if (claimed != a.Actor.UserId) return false;
        var db = Db(a);
        if (phase != Dev2AuthorizationPhase.Admission && db.Database.CurrentTransaction == null) return false;
        await using var admission = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable) return false;
        var users = await new UserEligibilityLock(db).LockAsync([a.Actor.UserId], ct);
        if (!users.TryGetValue(a.Actor.UserId, out var actor) || actor is not { DeletedAt: null, AccountStatus: "ACTIVE", EmploymentStatus: "ACTIVE" }) return false;
        if (!HotelOpsCookieTicket.TryRead(a.Http.User, out var ticket, out var sid) || ticket == null || ticket.IsRestricted
            || ticket.UserId != a.Actor.UserId || sid != a.Actor.SessionId) return false;
        var session = await a.Http.RequestServices.GetRequiredService<ISqlSessionService>().ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted) throw new Dev2AccessDenied(401);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var warehouse = await Warehouse(a, ct);
        if (!await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == a.Actor.UserId && r.Role.RoleCode == "STOREKEEPER"
            && r.IsActive && r.Role.IsActive && (r.BranchId == null || r.BranchId == warehouse.BranchId)
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && db.UserBranchAccesses.Any(b => b.UserId == a.Actor.UserId && b.BranchId == warehouse.BranchId
                && b.Branch.IsActive && b.GrantedAt <= now && b.RevokedAt == null), ct)) return false;
        long[] sourceIds;
        if (IsCreate(a))
        {
            var input = (ReplenishmentCreate)Input(a);
            if (input.Lines == null || input.Lines.Count == 0 || input.Lines.Any(l => l == null)) return false;
            sourceIds = input.Lines.Where(l => l.SourceMaterialRequestLineId != null).Select(l => l.SourceMaterialRequestLineId!.Value).Distinct().ToArray();
        }
        else
        {
            if (await db.MaterialReplenishmentLines.AnyAsync(l => l.ReplenishmentId == Id(a) && l.WarehouseId != warehouse.Id, ct)
                || await db.MaterialReplenishmentReceiptLines.AnyAsync(l => l.ReplenishmentLine.ReplenishmentId == Id(a)
                    && (l.StockDocumentLine.Document.DestinationWarehouseId != warehouse.Id || l.StockDocumentLine.Document.DocumentType != "RECEIPT"), ct)) return false;
            sourceIds = await db.MaterialReplenishmentLines.AsNoTracking().Where(l => l.ReplenishmentId == Id(a) && l.SourceMaterialRequestLineId != null)
                .Select(l => l.SourceMaterialRequestLineId!.Value).Distinct().ToArrayAsync(ct);
        }
        var sources = await db.MaterialRequestLines.AsNoTracking().Where(l => sourceIds.Contains(l.LineId))
            .Select(l => new { l.LineId, l.RequestId, l.Request.BranchId, l.Request.SourceWarehouseId }).ToArrayAsync(ct);
        if (sources.Length != sourceIds.Length || sources.Any(l => l.BranchId != warehouse.BranchId || l.SourceWarehouseId != warehouse.Id)) return false;
        var scope = await MaterialRequestReadAccess.ReadAsync(db, a.Actor.UserId, warehouse.BranchId, now, ct);
        foreach (var id in sources.Select(l => l.RequestId).Distinct())
            if (!await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == id, ct)
                || !await MaterialReadProvenance.ValidAsync(db, id, warehouse.BranchId, ct)) return false;
        return true;
    }
}
