using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService
{
    private async Task<InventoryPaged<TResult>> Page<TEntity, TResult>(IQueryable<TEntity> source,
        InventoryPage page, Func<TEntity, TResult> map, CancellationToken ct)
    {
        var (offset, size) = Paging(page);
        var count = await source.CountAsync(ct);
        var items = await source.Skip(offset).Take(size).ToListAsync(ct);
        return new(items.Select(map).ToList(), count, page.Page, size);
    }
    private static InventoryItemDto Dto(InventoryItem x) => new(x.ItemId, x.ItemCode, x.ItemName, x.CategoryId,
        x.BaseUomId, x.IsActive, x.IsConsumable, x.Description, x.Specification, Convert.ToBase64String(x.RowVersion));
    private static InventoryUnitDto Dto(UnitsOfMeasure x) => new(x.UomId, x.UomCode, x.UomName, x.IsActive, x.Description, Convert.ToBase64String(x.RowVersion));
    private static InventoryConversionDto Dto(UomConversion x) => new(x.ConversionId, x.ItemId, x.FromUomId, x.ToUomId, x.ConversionFactor, x.IsActive, Convert.ToBase64String(x.RowVersion));
    private static InventoryWarehouseDto Dto(Warehouse x) => new(x.WarehouseId, x.BranchId, x.DepartmentId, x.WarehouseCode, x.WarehouseName, x.ManagerUserId, x.LocationDescription, x.IsActive, Convert.ToBase64String(x.RowVersion));
    private static InventoryThresholdDto Dto(StockThreshold x) => new(x.ThresholdId, x.WarehouseId, x.ItemId, x.MinimumQty, x.MaximumQty, x.ReorderQty, x.AlertEnabled, Convert.ToBase64String(x.RowVersion));
    public Task<InventoryPaged<InventoryItemDto>> GetItems(InventoryPage page, CancellationToken ct) => Page(db.InventoryItems.AsNoTracking().OrderBy(x => x.ItemId), page, Dto, ct);
    public async Task<InventoryItemDto?> GetItem(long id, CancellationToken ct) => await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == id, ct) is { } x ? Dto(x) : null;
    public Task<List<InventoryLookup>> GetCategories(CancellationToken ct) => db.ItemCategories.AsNoTracking().OrderBy(x => x.ItemCategoryId).Select(x => new InventoryLookup(x.ItemCategoryId, x.CategoryCode, x.CategoryName, x.IsActive)).ToListAsync(ct);
    public Task<InventoryPaged<InventoryUnitDto>> GetUnits(InventoryPage page, CancellationToken ct) => Page(db.UnitsOfMeasures.AsNoTracking().OrderBy(x => x.UomId), page, Dto, ct);
    public async Task<InventoryUnitDto?> GetUnit(long id, CancellationToken ct) => await db.UnitsOfMeasures.AsNoTracking().SingleOrDefaultAsync(x => x.UomId == id, ct) is { } x ? Dto(x) : null;
    public async Task<InventoryConversionDto?> GetConversion(long item, long id, CancellationToken ct) => await db.UomConversions.AsNoTracking().SingleOrDefaultAsync(x => x.ItemId == item && x.ConversionId == id, ct) is { } x ? Dto(x) : null;
    public async Task<IReadOnlyList<InventoryConversionDto>?> GetConversions(long item, CancellationToken ct)
    {
        if (!await db.InventoryItems.AsNoTracking().AnyAsync(x => x.ItemId == item, ct)) return null;
        return (await db.UomConversions.AsNoTracking().Where(x => x.ItemId == item || x.ItemId == null).OrderBy(x => x.ConversionId).ToListAsync(ct)).Select(Dto).ToList();
    }
    public Task<InventoryPaged<InventoryWarehouseDto>> GetWarehouses(InventoryPage page, CancellationToken ct) => Page(db.Warehouses.AsNoTracking().OrderBy(x => x.WarehouseId), page, Dto, ct);
    public async Task<InventoryWarehouseDto?> GetWarehouse(long id, CancellationToken ct) => await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(x => x.WarehouseId == id, ct) is { } x ? Dto(x) : null;
    private IQueryable<InventoryStockDto> StockQuery()
    {
        var balances = db.StockMovements.GroupBy(x => new { x.WarehouseId, x.ItemId })
            .Select(g => new { g.Key.WarehouseId, g.Key.ItemId, OnHand = g.Sum(x => x.SignedBaseQty) });
        var pairs = balances.Select(x => new { x.WarehouseId, x.ItemId }).Union(db.StockThresholds.Select(x => new { x.WarehouseId, x.ItemId }));
        return from p in pairs
               join b in balances on new { p.WarehouseId, p.ItemId } equals new { b.WarehouseId, b.ItemId } into bg
               from b in bg.DefaultIfEmpty()
               join t in db.StockThresholds on new { p.WarehouseId, p.ItemId } equals new { t.WarehouseId, t.ItemId } into tg
               from t in tg.DefaultIfEmpty()
               select new InventoryStockDto
               {
                   WarehouseId = p.WarehouseId,
                   ItemId = p.ItemId,
                   OnHand = (decimal?)b.OnHand ?? 0,
                   Available = (decimal?)b.OnHand ?? 0,
                   MinimumQty = t == null ? null : t.MinimumQty,
                   BelowMinimum = t != null && ((decimal?)b.OnHand ?? 0) < t.MinimumQty
               };
    }
    public Task<InventoryPaged<InventoryStockDto>> GetStock(InventoryPage page, bool lowOnly, CancellationToken ct) =>
        Page(StockQuery().Where(x => !lowOnly || x.BelowMinimum).OrderBy(x => x.WarehouseId).ThenBy(x => x.ItemId), page, x => x, ct);
    public async Task<InventoryStockDto?> GetStock(long warehouse, long item, CancellationToken ct)
    {
        if (!await db.Warehouses.AsNoTracking().AnyAsync(x => x.WarehouseId == warehouse, ct) ||
            !await db.InventoryItems.AsNoTracking().AnyAsync(x => x.ItemId == item, ct)) return null;
        return await StockQuery().SingleOrDefaultAsync(x => x.WarehouseId == warehouse && x.ItemId == item, ct)
            ?? new(warehouse, item, 0, 0, null, false);
    }
    private IQueryable<InventoryMovementDto> MovementQuery() => db.StockMovements.AsNoTracking().Select(x => new InventoryMovementDto
    {
        MovementId = x.MovementId,
        DocumentLineId = x.DocumentLineId,
        DocumentId = x.DocumentLine.DocumentId,
        DocumentNo = x.DocumentLine.Document.DocumentNo,
        DocumentType = x.DocumentLine.Document.DocumentType,
        Status = x.DocumentLine.Document.Status,
        WarehouseId = x.WarehouseId,
        ItemId = x.ItemId,
        SignedBaseQty = x.SignedBaseQty,
        OccurredAt = x.OccurredAt
    });
    public Task<InventoryPaged<InventoryMovementDto>> GetMovements(InventoryPage page, CancellationToken ct) =>
        Page(MovementQuery().OrderBy(x => x.OccurredAt).ThenBy(x => x.MovementId), page, x => x, ct);
    public async Task<InventoryLedger?> GetLedger(long warehouse, long item, DateTime from, DateTime to, CancellationToken ct)
    {
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc || to < from) throw Invalid("From and To must be UTC and From <= To.");
        if (await GetStock(warehouse, item, ct) is null) return null;
        var source = db.StockMovements.AsNoTracking().Where(x => x.WarehouseId == warehouse && x.ItemId == item);
        var opening = await source.Where(x => x.OccurredAt < from).SumAsync(x => (decimal?)x.SignedBaseQty, ct) ?? 0;
        var movements = await MovementQuery().Where(x => x.WarehouseId == warehouse && x.ItemId == item && x.OccurredAt >= from && x.OccurredAt <= to)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.MovementId).ToListAsync(ct);
        return new(opening, movements, opening + movements.Sum(x => x.SignedBaseQty));
    }
    public async Task<InventoryDocumentDto?> GetDocument(long id, CancellationToken ct)
    {
        var x = await db.StockDocuments.AsNoTracking().Include(d => d.StockDocumentLines).ThenInclude(l => l.StockReceiptLineDetail)
            .Include(d => d.StockDocumentLines).ThenInclude(l => l.StockReturnLineDetailStockDocumentLine)
            .Include(d => d.StockTransferReceipts).ThenInclude(r => r.StockTransferReceiptLines).AsSplitQuery()
            .SingleOrDefaultAsync(d => d.DocumentId == id, ct);
        if (x is null) return null;
        var batches = x.StockTransferReceipts.OrderBy(r => r.TransferReceiptId).Select(r => new InventoryBatchDto(r.TransferReceiptId, r.ReceivedBy, r.ReceivedAt, r.Note,
            r.StockTransferReceiptLines.OrderBy(l => l.TransferReceiptLineId).Select(l => new InventoryBatchLineDto(l.TransferLineId, l.ReceivedQty, l.DamagedQty, l.MissingQty, l.Note)).ToList())).ToList();
        var canonical = x.DocumentType == "TRANSFER" && x.Status is "DRAFT" or "IN_TRANSIT" or "PARTIALLY_RECEIVED" or "RECEIVED" or "CANCELLED";
        var lines = x.StockDocumentLines.OrderBy(l => l.LineNo).Select(l =>
        {
            var receipts = batches.SelectMany(b => b.Lines).Where(r => r.TransferLineId == l.LineId).ToList();
            decimal? received = canonical ? receipts.Sum(r => r.ReceivedQty) : null;
            decimal? damaged = canonical ? receipts.Sum(r => r.DamagedQty) : l.StockReceiptLineDetail?.DamagedQty ?? l.StockReturnLineDetailStockDocumentLine?.DamagedQty;
            decimal? missing = canonical ? receipts.Sum(r => r.MissingQty) : null;
            decimal? transit = canonical ? (x.Status is "DRAFT" or "CANCELLED" ? 0 : l.ActualQty - received - damaged - missing) : null;
            return new InventoryLineDto(l.LineId, l.LineNo, l.ItemId, l.UomId, l.ConversionFactor, l.RequestedQty, l.ActualQty, l.BaseQty,
                l.StockReceiptLineDetail?.RejectedQty, damaged, l.StockReceiptLineDetail?.DiscrepancyReason ?? l.StockReturnLineDetailStockDocumentLine?.Note,
                l.StockReturnLineDetailStockDocumentLine?.OriginalIssueLineId, received, missing, transit);
        }).ToList();
        return new(x.DocumentId, x.DocumentNo, x.DocumentType, x.Status, x.SourceWarehouseId, x.DestinationWarehouseId, x.DocumentDate, x.Reason, x.Note,
            x.CreatedAt, x.PostedAt, x.CancelledAt, x.ReversalOfDocumentId, Convert.ToBase64String(x.RowVersion), lines, batches);
    }
    public async Task<InventoryPaged<InventoryDocumentDto>> GetDocuments(InventoryPage page, CancellationToken ct)
    {
        var (offset, size) = Paging(page); var q = db.StockDocuments.AsNoTracking();
        var count = await q.CountAsync(ct); var ids = await q.OrderBy(x => x.DocumentId).Skip(offset).Take(size).Select(x => x.DocumentId).ToListAsync(ct);
        var items = new List<InventoryDocumentDto>(); foreach (var id in ids) items.Add((await GetDocument(id, ct))!);
        return new(items, count, page.Page, size);
    }
    public async Task<IReadOnlyList<InventoryHistoryDto>?> GetHistory(long id, CancellationToken ct)
    {
        if (!await db.StockDocuments.AsNoTracking().AnyAsync(x => x.DocumentId == id, ct)) return null;
        var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "stock_documents" && x.EntityId == key)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.AuditId).Select(x => new InventoryHistoryDto(x.AuditId, x.BranchId, x.ActorUserId, x.Action, x.OccurredAt, x.OldValues, x.NewValues, x.ChangedFields)).ToListAsync(ct);
    }
    public async Task<InventoryStocktakeDto?> GetStocktake(long id, CancellationToken ct)
    {
        var x = await db.Stocktakes.AsNoTracking().Include(x => x.StocktakeLines).SingleOrDefaultAsync(x => x.StocktakeId == id, ct);
        return x is null ? null : new(x.StocktakeId, x.StocktakeNo, x.WarehouseId, x.SnapshotAt, x.Status, x.CountedBy, x.ApprovedBy, x.AdjustmentDocumentId, x.Note,
            Convert.ToBase64String(x.RowVersion), x.StocktakeLines.OrderBy(l => l.StocktakeLineId).Select(l => new InventoryStocktakeLineDto(l.StocktakeLineId, l.ItemId, l.SystemQty, l.CountedQty, l.VarianceQty, l.DiscrepancyReason, l.CountedBy, l.CountedAt)).ToList());
    }
    public async Task<InventoryPaged<InventoryStocktakeDto>> GetStocktakes(InventoryPage page, CancellationToken ct)
    {
        var (offset, size) = Paging(page); var count = await db.Stocktakes.CountAsync(ct);
        var ids = await db.Stocktakes.AsNoTracking().OrderBy(x => x.StocktakeId).Skip(offset).Take(size).Select(x => x.StocktakeId).ToListAsync(ct);
        var items = new List<InventoryStocktakeDto>(); foreach (var id in ids) items.Add((await GetStocktake(id, ct))!);
        return new(items, count, page.Page, size);
    }
}
