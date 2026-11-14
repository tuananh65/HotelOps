using HotelOps.Api.Services.Dev2Authorization;
using System.Globalization;
using System.Numerics;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using static HotelOps.Api.Services.Replenishments.ReplenishmentRules;

namespace HotelOps.Api.Services.Replenishments;

public sealed partial class ReplenishmentService(HotelOpsDbContext db, IInventoryReceiptPosting posting, IInventoryQuantityContext quantities, IHttpContextAccessor? authorityHttp = null)
{
    private readonly IHttpContextAccessor? _authorityHttp = authorityHttp;
    private long TrustedActorId => Dev2ServiceAuthority.ActorId(_authorityHttp);

    private async Task Catalog(CancellationToken ct) => await Gate(db, "HotelOps:Inventory:Catalog", ct);
    private async Task<Warehouse> Warehouse(long id, bool locking, CancellationToken ct)
    {
        if (locking) await Gate(db, $"HotelOps:Inventory:Warehouse:{id}", ct);
        var query = locking ? db.Warehouses.FromSqlInterpolated($"SELECT * FROM dbo.warehouses WITH (UPDLOCK,HOLDLOCK) WHERE warehouse_id={id}") : db.Warehouses.AsNoTracking().Where(x => x.WarehouseId == id);
        var w = await query.SingleOrDefaultAsync(ct);
        if (w is null || !w.IsActive || !await db.Branches.AnyAsync(x => x.BranchId == w.BranchId && x.IsActive, ct)) throw Invalid("Warehouse/branch must exist and be active.");
        return w;
    }
    private async Task<long> BaseUom(long item, CancellationToken ct)
    {
        var i = await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == item, ct) ?? throw Invalid("Item is missing.");
        return (await quantities.ConvertToBase(item, i.BaseUomId, 1, ct)).BaseUomId;
    }
    private static void Open(MaterialReplenishment r)
    {
        if (r.Status is not ("REQUESTED" or "PARTIALLY_RECEIVED")) throw Invalid("Replenishment is terminal for user commands.");
    }
    private async Task<string> Number(CancellationToken ct)
    {
        while (true)
        {
            var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture); var prefix = "RPL-" + date + "-";
            await Gate(db, "HotelOps:Replenishment:Number:RPL-" + date, ct);
            if (date != DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)) continue;
            var numbers = await db.MaterialReplenishments.Where(x => x.ReplenishmentNo.StartsWith(prefix)).Select(x => x.ReplenishmentNo).ToListAsync(ct); BigInteger maximum = 0;
            foreach (var n in numbers) { var suffix = n[prefix.Length..]; if (suffix.Length > 0 && suffix.All(char.IsAsciiDigit) && BigInteger.TryParse(suffix, out var value)) maximum = BigInteger.Max(maximum, value); }
            var result = prefix + (maximum + 1).ToString("D3", CultureInfo.InvariantCulture);
            return result.Length <= 50 ? result : throw new InvalidOperationException("Replenishment number space exhausted.");
        }
    }
    public async Task<ReplenishmentDto> Create(ReplenishmentCreate input, CancellationToken ct)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Replenishments", ["Create"], input.ActorUserId, null, input);
        await using var tx = await Dev2ServiceAuthority.BeginTransactionAsync(db, _authorityHttp, ct);
        try
        {
            await Catalog(ct); var warehouse = await Warehouse(input.WarehouseId, false, ct);
            await Actor(db, TrustedActorId, warehouse.BranchId, ct);
            var supplier = input.SupplierName?.Trim(); if (supplier?.Length > 200) throw Invalid("SupplierName exceeds 200 characters.");
            if (input.Lines is null || input.Lines.Count == 0 || input.Lines.Any(x => x is null) || input.Lines.Any(x => x.LineNo <= 0) || input.Lines.Select(x => x.LineNo).Distinct().Count() != input.Lines.Count || input.Lines.Select(x => x.ItemId).Distinct().Count() != input.Lines.Count) throw Invalid("Nonempty lines with unique positive LineNo and unique ItemId per warehouse are required.");
            var sources = input.Lines.Where(x => x.SourceMaterialRequestLineId != null).Select(x => x.SourceMaterialRequestLineId!.Value).ToArray();
            var requestIds = await db.MaterialRequestLines.Where(x => sources.Contains(x.LineId)).Select(x => x.RequestId).Distinct().OrderBy(x => x).ToListAsync(ct);
            foreach (var id in requestIds)
            {
                await Gate(db, $"HotelOps:MaterialRequest:Request:{id}", ct);
                await db.MaterialRequests.FromSqlInterpolated($"SELECT * FROM dbo.material_requests WITH (UPDLOCK,HOLDLOCK) WHERE request_id={id}").SingleAsync(ct);
            }
            var root = new MaterialReplenishment { WarehouseId = warehouse.WarehouseId, Warehouse = warehouse, RequestedBy = TrustedActorId, SupplierName = string.IsNullOrEmpty(supplier) ? null : supplier, ExpectedOn = input.ExpectedOn, Status = "REQUESTED", CreatedAt = DateTime.UtcNow };
            foreach (var l in input.Lines) root.MaterialReplenishmentLines.Add(new() { LineNo = l.LineNo, WarehouseId = warehouse.WarehouseId, ItemId = l.ItemId, BaseUomId = await BaseUom(l.ItemId, ct), SourceType = l.SourceType, SourceThresholdId = l.SourceThresholdId, SourceMaterialRequestLineId = l.SourceMaterialRequestLineId, RequestedBaseQty = Quantity(l.RequestedBaseQty), IsOpenCoverage = true, Note = l.Note?.Trim() });
            await Claims(db, root.MaterialReplenishmentLines, ct);
            root.Warehouse = await Warehouse(warehouse.WarehouseId, true, ct);
            foreach (var l in root.MaterialReplenishmentLines.OrderBy(x => x.ItemId))
            {
                await Gate(db, $"HotelOps:Inventory:Stock:{warehouse.WarehouseId}:{l.ItemId}", ct);
                if (await db.MaterialReplenishmentLines.AnyAsync(x => x.WarehouseId == warehouse.WarehouseId && x.ItemId == l.ItemId && x.IsOpenCoverage, ct)) throw Invalid("Active Warehouse/Item coverage already exists.");
                if (l.SourceType == "LOW_STOCK" && l.SourceThresholdId != null && l.SourceMaterialRequestLineId == null)
                {
                    var t = await db.StockThresholds.AsNoTracking().SingleOrDefaultAsync(x => x.ThresholdId == l.SourceThresholdId && x.WarehouseId == warehouse.WarehouseId && x.ItemId == l.ItemId, ct);
                    if (t is null || await quantities.ObserveStock(warehouse.WarehouseId, l.ItemId, ct) >= t.MinimumQty) throw Invalid("Threshold missing or warehouse/item no longer below minimum.");
                }
                else if (l.SourceType == "MATERIAL_REQUEST_SHORTAGE" && l.SourceMaterialRequestLineId != null && l.SourceThresholdId == null)
                {
                    var candidates = await MaterialCandidates(warehouse.WarehouseId, l.ItemId, ct);
                    var source = candidates.SingleOrDefault(x => x.RequestLineId == l.SourceMaterialRequestLineId);
                    if (source is null || source.BranchId != warehouse.BranchId || source.PhysicalShortageBaseQty <= 0 || l.RequestedBaseQty > source.RemainingBaseQty) throw Invalid("MR source is legacy, no longer eligible, sufficiently stocked or requested quantity exceeds source remaining base demand.");
                }
                else throw Invalid("Exactly one valid typed shortage source is required.");
            }
            root.ReplenishmentNo = await Number(ct);
            // Keep header/line Save boundaries observable by SQL fault-injection tests.
            var lines = root.MaterialReplenishmentLines.ToArray(); root.MaterialReplenishmentLines.Clear(); db.MaterialReplenishments.Add(root); await Dev2ServiceAuthority.SaveChangesAsync(db, _authorityHttp, ct);
            foreach (var l in lines) root.MaterialReplenishmentLines.Add(l); await Dev2ServiceAuthority.SaveChangesAsync(db, _authorityHttp, ct);
            Audit(db, root, TrustedActorId, "REPLENISHMENT_CREATED", null);
            await Dev2ServiceAuthority.SaveChangesAsync(db, _authorityHttp, ct); await Dev2ServiceAuthority.CommitAsync(db, _authorityHttp, tx, ct); return (await Get(root.ReplenishmentId, ct))!;
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    public async Task<ReplenishmentDto?> Close(long id, ReplenishmentClose input, CancellationToken ct)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Replenishments", ["Close"], input.ActorUserId, id, input);
        await using var tx = await Dev2ServiceAuthority.BeginTransactionAsync(db, _authorityHttp, ct);
        try
        {
            await Catalog(ct); var r = await Owner(db, id, ct); if (r is null) return null;
            Version(r.RowVersion, input.ExpectedRowVersion); Open(r); await Actor(db, TrustedActorId, r.Warehouse.BranchId, ct); await Reconcile(db, r, ct); await Claims(db, r.MaterialReplenishmentLines, ct);
            if (!r.MaterialReplenishmentLines.Any(x => x.RequestedBaseQty > x.ReceivedBaseQty)) throw Invalid("Positive remaining is required.");
            var reason = input.Reason?.Trim(); if (reason is not { Length: >= 10 }) throw Invalid("Close reason must contain at least 10 characters.");
            var before = Snapshot(r); r.Status = "CLOSED"; r.CloseReason = reason; r.ClosedBy = TrustedActorId; r.ClosedAt = r.UpdatedAt = DateTime.UtcNow;
            foreach (var l in r.MaterialReplenishmentLines) l.IsOpenCoverage = false;
            Audit(db, r, TrustedActorId, "REMAINDER_CLOSED", before); await Dev2ServiceAuthority.SaveChangesAsync(db, _authorityHttp, ct); await Dev2ServiceAuthority.CommitAsync(db, _authorityHttp, tx, ct); return await Get(id, ct);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    public async Task<ReplenishmentDto?> Receive(long id, ReplenishmentLink input, CancellationToken ct, ReplenishmentPost? post = null)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Replenishments", [post is null ? "Link" : "Post"], input.ActorUserId, id, (object?)post ?? input);
        if (post is not null) Dev2ServiceAuthority.RequireClaim(_authorityHttp, post.ActorUserId);
        await using var tx = await Dev2ServiceAuthority.BeginTransactionAsync(db, _authorityHttp, ct);
        try
        {
            await Catalog(ct);
            if (!await db.MaterialReplenishments.AnyAsync(x => x.ReplenishmentId == id, ct)) return null;
            if (input.Lines is null || input.Lines.Count == 0 || input.Lines.Any(x => x is null) || input.Lines.Select(x => x.StockDocumentLineId).Distinct().Count() != input.Lines.Count) throw Invalid("Distinct receipt-line mappings are required.");
            var lineIds = input.Lines.Select(x => x.StockDocumentLineId).ToArray();
            var documents = await db.StockDocumentLines.Where(x => lineIds.Contains(x.LineId)).Select(x => x.DocumentId).ToListAsync(ct);
            if (documents.Count != lineIds.Length) throw Invalid("Referenced receipt line is missing.");
            if (post != null) documents.Add(post.ReceiptDocumentId);
            foreach (var doc in documents.Distinct().Order())
            {
                await Gate(db, $"HotelOps:Inventory:Document:{doc}", ct);
                await db.StockDocuments.FromSqlInterpolated($"SELECT * FROM dbo.stock_documents WITH (UPDLOCK,HOLDLOCK) WHERE document_id={doc}").SingleAsync(ct);
            }
            var r = (await Owner(db, id, ct))!; Version(r.RowVersion, input.ExpectedRowVersion); Open(r); await Actor(db, TrustedActorId, r.Warehouse.BranchId, ct);
            await Reconcile(db, r, ct); await Claims(db, r.MaterialReplenishmentLines, ct);
            var before = Snapshot(r);
            if (post != null)
            {
                if (documents.Any(x => x != post.ReceiptDocumentId)) throw Invalid("Coordinated mappings must belong to ReceiptDocumentId.");
                await Dev2ServiceAuthority.RevalidateAsync(db, _authorityHttp, Dev2AuthorizationPhase.BeforeStockPosting, ct);
                await posting.PostReceipt(post.ReceiptDocumentId, new(TrustedActorId, post.ReceiptExpectedRowVersion, post.ReceiptLines), ct);
            }
            await Warehouse(r.WarehouseId, true, ct);
            var linked = new List<object>();
            foreach (var mapping in input.Lines)
            {
                var l = r.MaterialReplenishmentLines.SingleOrDefault(x => x.ReplenishmentLineId == mapping.ReplenishmentLineId) ?? throw Invalid("Mapping does not belong to replenishment.");
                var line = await db.StockDocumentLines.Include(x => x.Document).SingleAsync(x => x.LineId == mapping.StockDocumentLineId, ct);
                if (line.Document.DocumentType != "RECEIPT" || line.Document.Status != "POSTED" || line.Document.DestinationWarehouseId != r.WarehouseId || line.ItemId != l.ItemId) throw Invalid("Only matching POSTED receipt lines may be credited.");
                if (post != null && line.BaseQty == 0) continue; // rejected-only delivery persists in Inventory but gives no positive credit.
                await ProveReceipt(db, line, r.WarehouseId, l.ItemId, l.BaseUomId, ct);
                if (await db.MaterialReplenishmentReceiptLines.AnyAsync(x => x.StockDocumentLineId == line.LineId, ct)) throw Invalid("Receipt line already credited globally.");
                db.MaterialReplenishmentReceiptLines.Add(new() { ReplenishmentLineId = l.ReplenishmentLineId, StockDocumentLineId = line.LineId, CreditedBaseQty = line.BaseQty, LinkedAt = DateTime.UtcNow, LinkedBy = TrustedActorId });
                linked.Add(new { l.ReplenishmentLineId, StockDocumentId = line.DocumentId, StockDocumentLineId = line.LineId, CreditedBaseQty = line.BaseQty });
            }
            await Dev2ServiceAuthority.SaveChangesAsync(db, _authorityHttp, ct);
            await Refresh(db, r, null, ct); await NoConflictingClaims(db, [r], ct);
            Audit(db, r, TrustedActorId, "RECEIPT_LINKED", before, new { Links = linked });
            if (r.Status == "FULLY_RECEIVED" || r.Status == "PARTIALLY_RECEIVED") Audit(db, r, TrustedActorId, r.Status == "FULLY_RECEIVED" ? "REPLENISHMENT_FULLY_RECEIVED" : "REPLENISHMENT_PARTIALLY_RECEIVED", before, new { Links = linked });
            await Dev2ServiceAuthority.SaveChangesAsync(db, _authorityHttp, ct); await Dev2ServiceAuthority.CommitAsync(db, _authorityHttp, tx, ct); return await Get(id, ct);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
}
