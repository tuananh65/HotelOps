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

internal static partial class ReplenishmentWriteContract
{
    private const string Policy = "UC100/101 create/close/link: HTTPS unrestricted SQL session; active STOREKEEPER and current branch access; "
        + "independent REPLENISHMENT_CREATE/CLOSE/RECEIVE plus REPLENISHMENT_VIEW, INVENTORY_VIEW for shortage creation and receipt linking; "
        + "link only existing POSTED RECEIPT lines of the same warehouse/item; coordinated Post additionally requires STOCK_RECEIPT_POST, exact receipt lineage and Draft-to-Posted phase checks; "
        + "independent MR_VIEW and same-warehouse material provenance; exact principal/input/root binding; "
        + "Serializable action through persistence/disclosure, existing coverage/concurrency/reconciliation/audit preserved";
    internal static IEnumerable<Dev2ActionContract> Contracts => new[] { "A004A-056", "A004A-057", "A004A-058", "A004A-059" }.Select(id => new Dev2ActionContract(
        id, "DEV3_142A_REPLENISHMENT_WRITE_V1", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Policy))),
        async (a, ct) => {
            var branch = (await Warehouse(a, ct)).BranchId;
            var requirements = new List<Dev2PermissionRequirement> {
                new(IsCreate(a) ? "REPLENISHMENT_CREATE" : IsLink(a) || IsPost(a) ? "REPLENISHMENT_RECEIVE" : "REPLENISHMENT_CLOSE", new(Dev2PermissionTargetKind.Branch, branch)),
                new("REPLENISHMENT_VIEW", new(Dev2PermissionTargetKind.Branch, branch)) };
            if (IsCreate(a) || IsLink(a) || IsPost(a)) requirements.Add(new("INVENTORY_VIEW", new(Dev2PermissionTargetKind.Branch, branch)));
            if (IsPost(a)) requirements.Add(new("STOCK_RECEIPT_POST", new(Dev2PermissionTargetKind.Branch, branch)));
            return requirements;
        }, Dev2PermissionCombination.All, Validate,
        (a, call) => call.Controller == "Replenishments" && call.Action == (IsCreate(a) ? "Create" : IsLink(a) ? "Link" : IsPost(a) ? "Post" : "Close")
            && call.RootId == (IsCreate(a) ? null : Id(a)) && ReferenceEquals(call.Input, Input(a)),
        null, null, Validate, RequiresActionTransaction: true));

    private static bool IsCreate(Dev2BoundAction a) => a.Identity.DecisionId == "A004A-056";
    private static bool IsLink(Dev2BoundAction a) => a.Identity.DecisionId == "A004A-058";
    private static bool IsPost(Dev2BoundAction a) => a.Identity.DecisionId == "A004A-059";
    private static HotelOpsDbContext Db(Dev2BoundAction a) => a.Http.RequestServices.GetRequiredService<HotelOpsDbContext>();
    private static long Id(Dev2BoundAction a) => a.Arguments.GetValueOrDefault("id") is long id && id > 0 ? id : throw new Dev2AccessDenied(404);
    private static object Input(Dev2BoundAction a) => IsCreate(a)
        ? a.Arguments.GetValueOrDefault("input") as ReplenishmentCreate ?? throw new Dev2AccessDenied(400)
        : IsLink(a) ? a.Arguments.GetValueOrDefault("input") as ReplenishmentLink ?? throw new Dev2AccessDenied(400)
        : IsPost(a) ? a.Arguments.GetValueOrDefault("input") as ReplenishmentPost ?? throw new Dev2AccessDenied(400)
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
            || !(IsCreate(a) && a.Identity.ActionName == "Create" || IsLink(a) && a.Identity.ActionName == "Link"
                || IsPost(a) && a.Identity.ActionName == "Post"
                || a.Identity.DecisionId == "A004A-057" && a.Identity.ActionName == "Close")) return false;
        var claimed = Input(a) switch { ReplenishmentCreate create => create.ActorUserId,
            ReplenishmentLink link => link.ActorUserId, ReplenishmentPost post => post.ActorUserId, ReplenishmentClose close => close.ActorUserId, _ => 0 };
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
        if (IsLink(a))
        {
            var mapping = ((ReplenishmentLink)Input(a)).Lines;
            if (mapping == null || mapping.Count == 0 || mapping.Any(m => m == null || m.StockDocumentLineId <= 0 || m.ReplenishmentLineId <= 0)
                || mapping.Select(m => m.StockDocumentLineId).Distinct().Count() != mapping.Count) return false;
            foreach (var line in mapping)
                if (!await db.MaterialReplenishmentLines.AsNoTracking().AnyAsync(r => r.ReplenishmentLineId == line.ReplenishmentLineId
                    && r.ReplenishmentId == Id(a) && r.WarehouseId == warehouse.Id
                    && db.StockDocumentLines.Any(s => s.LineId == line.StockDocumentLineId && s.ItemId == r.ItemId
                        && s.Document.DocumentType == "RECEIPT" && s.Document.Status == "POSTED"
                        && s.Document.DestinationWarehouseId == warehouse.Id
                        && s.Document.PostedAt != null && s.Document.PostedBy != null), ct)) return false;
        }
        return !IsPost(a) || await ValidatePost(a, phase, warehouse, ct);
    }
}
