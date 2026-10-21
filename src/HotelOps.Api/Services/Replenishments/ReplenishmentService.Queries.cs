using HotelOps.Api.DTOs.Replenishments;
using Microsoft.EntityFrameworkCore;
using static HotelOps.Api.Services.Replenishments.ReplenishmentRules;

namespace HotelOps.Api.Services.Replenishments;

public sealed partial class ReplenishmentService
{
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;
    private static int Offset(ReplenishmentPage p)
    {
        if (p.Page < 1 || p.PageSize is < 1 or > 100 || (long)(p.Page - 1) * p.PageSize > int.MaxValue || p.ExpectedFrom > p.ExpectedTo || p.CreatedFrom > p.CreatedTo) throw Invalid("Invalid paging/date range.");
        return (p.Page - 1) * p.PageSize;
    }
    public async Task<ReplenishmentPaged<ReplenishmentSummary>> List(ReplenishmentPage p, CancellationToken ct)
    {
        var offset = Offset(p); var q = db.MaterialReplenishments.AsNoTracking();
        if (p.BranchId is long b) q = q.Where(x => x.Warehouse.BranchId == b);
        if (p.WarehouseId is long w) q = q.Where(x => x.WarehouseId == w);
        if (p.ItemId is long item) q = q.Where(x => x.MaterialReplenishmentLines.Any(l => l.ItemId == item));
        if (!string.IsNullOrWhiteSpace(p.Status)) q = q.Where(x => x.Status == p.Status);
        if (!string.IsNullOrWhiteSpace(p.SourceType)) q = q.Where(x => x.MaterialReplenishmentLines.Any(l => l.SourceType == p.SourceType));
        if (!string.IsNullOrWhiteSpace(p.Supplier)) q = q.Where(x => x.SupplierName != null && x.SupplierName.Contains(p.Supplier));
        if (!string.IsNullOrWhiteSpace(p.Keyword)) q = q.Where(x => x.ReplenishmentNo.Contains(p.Keyword) || x.SupplierName != null && x.SupplierName.Contains(p.Keyword));
        if (p.ExpectedFrom is DateOnly ef) q = q.Where(x => x.ExpectedOn >= ef);
        if (p.ExpectedTo is DateOnly et) q = q.Where(x => x.ExpectedOn <= et);
        if (p.CreatedFrom is DateTime cf) q = q.Where(x => x.CreatedAt >= cf);
        if (p.CreatedTo is DateTime ce) q = q.Where(x => x.CreatedAt <= ce);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.ExpectedOn == null).ThenBy(x => x.ExpectedOn).ThenBy(x => x.ReplenishmentId).Skip(offset).Take(p.PageSize)
            .Select(x => new ReplenishmentSummary(x.ReplenishmentId, x.ReplenishmentNo, x.WarehouseId, x.Warehouse.BranchId, x.Status, x.SupplierName, x.ExpectedOn, x.CreatedAt)).ToListAsync(ct);
        return new(p.Page, p.PageSize, total, rows.Select(x => x with { CreatedAt = Utc(x.CreatedAt) }).ToArray());
    }
    private async Task<ReplenishmentDto?> Read(long id, CancellationToken ct)
    {
        var r = await db.MaterialReplenishments.AsNoTracking().Include(x => x.Warehouse).Include(x => x.MaterialReplenishmentLines).ThenInclude(x => x.SourceMaterialRequestLine).SingleOrDefaultAsync(x => x.ReplenishmentId == id, ct);
        if (r is null) return null;
        await Reconcile(db, r, ct);
        var links = await Links(db, id, ct); var originals = links.Select(x => x.StockDocumentLine.DocumentId).Distinct().ToArray();
        var reversals = await db.StockDocuments.AsNoTracking().Where(x => x.ReversalOfDocumentId != null && originals.Contains(x.ReversalOfDocumentId.Value) && x.DocumentType == "REVERSAL" && x.Status == "POSTED").Select(x => new { Original = x.ReversalOfDocumentId!.Value, x.DocumentId }).ToListAsync(ct);
        return new(r.ReplenishmentId, r.ReplenishmentNo, r.Status, Convert.ToBase64String(r.RowVersion), r.WarehouseId, r.Warehouse.BranchId, r.RequestedBy, r.SupplierName, r.ExpectedOn, Utc(r.CreatedAt), Utc(r.UpdatedAt), Utc(r.ClosedAt), r.ClosedBy, r.CloseReason,
            r.MaterialReplenishmentLines.OrderBy(x => x.LineNo).Select(x => new ReplenishmentLineDto(x.ReplenishmentLineId, x.LineNo, x.ItemId, x.BaseUomId, x.SourceType, x.SourceThresholdId, x.SourceMaterialRequestLineId, x.SourceMaterialRequestLine?.RequestId,
                x.RequestedBaseQty, links.Where(l => l.ReplenishmentLineId == x.ReplenishmentLineId).Sum(l => l.CreditedBaseQty), x.ReceivedBaseQty, x.RequestedBaseQty - x.ReceivedBaseQty, x.IsOpenCoverage, x.Note)).ToArray(),
            links.OrderBy(x => x.ReplenishmentReceiptLineId).Select(x => new ReplenishmentReceiptDto(x.ReplenishmentReceiptLineId, x.ReplenishmentLineId, x.StockDocumentLine.DocumentId, x.StockDocumentLine.Document.DocumentNo, x.StockDocumentLineId,
                x.CreditedBaseQty, x.StockDocumentLine.Document.Status == "POSTED" ? x.CreditedBaseQty : 0, x.StockDocumentLine.Document.Status, reversals.SingleOrDefault(l => l.Original == x.StockDocumentLine.DocumentId)?.DocumentId, Utc(x.LinkedAt), x.LinkedBy)).ToArray());
    }
    public async Task<ReplenishmentDto?> Get(long id, CancellationToken ct)
    {
        // RCSI/SNAPSHOT are off locally: short Catalog-owned read gives a consistent multi-query aggregate.
        if (db.Database.CurrentTransaction != null) return await Read(id, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await Catalog(ct); var r = await Read(id, ct); await tx.CommitAsync(ct); return r;
    }
    public async Task<IReadOnlyList<ReplenishmentReceiptDto>?> Receipts(long id, CancellationToken ct) => (await Get(id, ct))?.Receipts;
    public async Task<ReplenishmentHistory?> History(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await Catalog(ct); var r = await Read(id, ct); if (r is null) return null;
        var docs = r.Receipts.Select(x => x.StockDocumentId.ToString()).Concat(r.Receipts.Where(x => x.ReversalDocumentId != null).Select(x => x.ReversalDocumentId!.Value.ToString())).ToArray();
        var rows = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "material_replenishments" && x.EntityId == id.ToString() || x.EntityType == "stock_documents" && docs.Contains(x.EntityId))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.AuditId).Select(x => new ReplenishmentAuditDto(x.AuditId, x.EntityType, x.EntityId, x.Action, x.ActorUserId, x.BranchId, x.OccurredAt, x.OldValues, x.NewValues, x.ChangedFields)).ToListAsync(ct);
        await tx.CommitAsync(ct); return new(r, rows.Select(x => x with { OccurredAt = Utc(x.OccurredAt) }).ToArray());
    }
    public async Task<ReplenishmentPaged<LowStockShortage>> LowStock(ReplenishmentPage p, CancellationToken ct)
    {
        var offset = Offset(p); await using var tx = await db.Database.BeginTransactionAsync(ct); await Catalog(ct);
        var thresholds = await db.StockThresholds.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Item).ThenInclude(x => x.BaseUom)
            .Where(x => x.Warehouse.IsActive && x.Warehouse.Branch.IsActive && x.Item.IsActive && x.Item.Category.IsActive && x.Item.BaseUom.IsActive && (p.WarehouseId == null || x.WarehouseId == p.WarehouseId) && (p.BranchId == null || x.Warehouse.BranchId == p.BranchId) && (p.ItemId == null || x.ItemId == p.ItemId)).OrderBy(x => x.WarehouseId).ThenBy(x => x.ItemId).ToListAsync(ct);
        var result = new List<LowStockShortage>();
        foreach (var t in thresholds)
        {
            var balance = await quantities.ObserveStock(t.WarehouseId, t.ItemId, ct); if (balance >= t.MinimumQty) continue;
            var coverage = await db.MaterialReplenishmentLines.AsNoTracking().Include(x => x.MaterialReplenishment).SingleOrDefaultAsync(x => x.WarehouseId == t.WarehouseId && x.ItemId == t.ItemId && x.IsOpenCoverage, ct);
            result.Add(new(t.WarehouseId, t.Warehouse.BranchId, t.ItemId, t.Item.BaseUomId, balance, t.MinimumQty, t.MinimumQty - balance, t.ThresholdId, coverage?.ReplenishmentId, coverage?.MaterialReplenishment.Status));
        }
        await tx.CommitAsync(ct); return new(p.Page, p.PageSize, result.Count, result.Skip(offset).Take(p.PageSize).ToArray());
    }
    private async Task<List<MaterialRequestShortage>> MaterialCandidates(long? warehouse, long? item, CancellationToken ct)
    {
        var lines = await db.MaterialRequestLines.AsNoTracking().Include(x => x.Request).ThenInclude(x => x.SourceWarehouse)
            .Where(x => x.IsActive && x.ApprovedQty > x.FulfilledQty && (x.Request.Status == "APPROVED" || x.Request.Status == "PARTIALLY_ISSUED") && x.Request.SourceWarehouseId != null
                && x.Request.SourceWarehouse!.IsActive && x.Request.SourceWarehouse.BranchId == x.Request.BranchId && x.Item.IsActive && x.Item.Category.IsActive && x.Uom.IsActive
                && (warehouse == null || x.Request.SourceWarehouseId == warehouse) && (item == null || x.ItemId == item)
                && db.MaterialRequestApprovalCycles.Any(c => c.RequestId == x.RequestId && c.Status == "APPROVED" && !db.MaterialRequestApprovalCycles.Any(later => later.RequestId == c.RequestId && later.RevisionNo > c.RevisionNo)))
            .OrderBy(x => x.RequestId).ThenBy(x => x.LineId).ToListAsync(ct);
        var result = new List<MaterialRequestShortage>();
        foreach (var l in lines)
        {
            var remaining = l.ApprovedQty!.Value - l.FulfilledQty; var converted = await quantities.ConvertToBase(l.ItemId, l.UomId, remaining, ct);
            var stock = await quantities.ObserveStock(l.Request.SourceWarehouseId!.Value, l.ItemId, ct);
            var claim = await db.MaterialReplenishmentLines.Where(x => x.WarehouseId == l.Request.SourceWarehouseId && x.ItemId == l.ItemId && x.IsOpenCoverage).Select(x => (long?)x.ReplenishmentId).SingleOrDefaultAsync(ct);
            result.Add(new(l.RequestId, l.Request.RequestNo, l.LineId, l.Request.BranchId, l.Request.SourceWarehouseId.Value, l.ItemId, l.UomId, converted.BaseUomId, l.ApprovedQty.Value, l.FulfilledQty, remaining, converted.BaseQty, stock, 0, 0, claim));
        }
        // Physical context aggregates observable demand, not reservations or suggested order quantities.
        return result.GroupBy(x => (x.WarehouseId, x.ItemId)).SelectMany(g => g.Select(x => x with { AggregateApprovedDemandBaseQty = g.Sum(y => y.RemainingBaseQty), PhysicalShortageBaseQty = Math.Max(0, g.Sum(y => y.RemainingBaseQty) - x.CurrentOnHand) })).Where(x => x.PhysicalShortageBaseQty > 0).ToList();
    }
    public async Task<ReplenishmentPaged<MaterialRequestShortage>> MaterialShortages(ReplenishmentPage p, CancellationToken ct)
    {
        var offset = Offset(p); await using var tx = await db.Database.BeginTransactionAsync(ct); await Catalog(ct);
        var rows = (await MaterialCandidates(p.WarehouseId, p.ItemId, ct)).Where(x => p.BranchId == null || x.BranchId == p.BranchId).ToArray();
        await tx.CommitAsync(ct); return new(p.Page, p.PageSize, rows.Length, rows.Skip(offset).Take(p.PageSize).ToArray());
    }
}
