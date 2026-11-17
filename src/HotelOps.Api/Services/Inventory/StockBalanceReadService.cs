using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class StockBalanceQuery
{
    [Range(1, long.MaxValue)] public long? WarehouseId { get; set; }
    [Range(1, long.MaxValue)] public long? ItemId { get; set; }
    [Range(1, long.MaxValue)] public long? CategoryId { get; set; }
    public bool BelowMinimum { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
}
public sealed record StockReadOption(long Id, string Name);
public sealed record StockBalanceRow(long WarehouseId, long ItemId, string Item, string Unit,
    decimal Available, decimal OutboundInTransit, decimal InboundInTransit, decimal? Minimum, bool BelowMinimum);
public sealed record StockLedgerRow(long MovementId, long DocumentId, string Document, string Type,
    DateTimeOffset At, decimal Quantity, decimal RunningBalance);
public sealed record StockBalanceResult(DateTimeOffset Snapshot, StockBalanceQuery Filters,
    StockReadOption[] Warehouses, StockReadOption[] Categories, StockBalanceRow[] Items, int TotalCount,
    decimal? OpeningBalance, decimal? ClosingBalance, StockLedgerRow[] Ledger);

// UC089 is a read-only projection. Source movements and immutable transfer
// conversion factors remain authoritative; this service never posts stock.
public sealed class StockBalanceReadService(HotelOpsDbContext db, InsightAccess access, DashboardSessionGuard session)
{
    public async Task<HotelOps.Api.DTOs.Inventory.InventoryDocumentDto> DocumentAsync(long id, CancellationToken ct)
    {
        if (id <= 0) throw new InsightException(400, "INVALID_DOCUMENT", "Invalid document identifier.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await new Users.UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        var branches = await Branches(ct);
        var root = await db.StockDocuments.AsNoTracking().Where(d => d.DocumentId == id)
            .Select(d => new { Source = d.SourceWarehouse == null ? (long?)null : d.SourceWarehouse.BranchId,
                Destination = d.DestinationWarehouse == null ? (long?)null : d.DestinationWarehouse.BranchId }).SingleOrDefaultAsync(ct);
        var required = root == null ? [] : new[] { root.Source, root.Destination }.Where(b => b.HasValue).Select(b => b!.Value).Distinct().ToArray();
        if (required.Length == 0 || required.Except(branches).Any())
            throw new InsightException(404, "DOCUMENT_UNAVAILABLE", "Document unavailable in current scope.");
        var result = await new InventoryService(db).GetDocument(id, ct)
            ?? throw new InsightException(404, "DOCUMENT_UNAVAILABLE", "Document unavailable.");
        if (required.Except(await Branches(ct)).Any()) throw new InsightException(403, "ACCESS_DENIED", "Document authority changed.");
        await session.RequireAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private IQueryable<StockMovement> Posted() => db.StockMovements.AsNoTracking().Where(m =>
        m.ItemId == m.DocumentLine.ItemId &&
        (m.DocumentLine.Document.Status == "POSTED" || m.DocumentLine.Document.Status == "REVERSED"
         || m.DocumentLine.Document.DocumentType == "TRANSFER" &&
            (m.DocumentLine.Document.Status == "IN_TRANSIT" || m.DocumentLine.Document.Status == "PARTIALLY_RECEIVED"
             || m.DocumentLine.Document.Status == "RECEIVED")));

    private async Task<long[]> Branches(CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var user = access.UserId;
        var permitted = await access.BranchesAsync("INVENTORY_VIEW", null, ct);
        return await db.Branches.AsNoTracking().Where(b => permitted.Contains(b.BranchId) && b.IsActive
            && db.UserBranchAccesses.Any(a => a.UserId == user && a.BranchId == b.BranchId && a.RevokedAt == null && a.GrantedAt <= now)
            && db.UserRoles.Any(r => r.UserId == user && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE"
                && r.User.DeletedAt == null && r.IsActive && r.Role.IsActive && r.AssignedAt <= now
                && (r.ExpiresAt == null || r.ExpiresAt > now) && (r.BranchId == null || r.BranchId == b.BranchId)
                && (r.Role.RoleCode == "STOREKEEPER" || r.Role.RoleCode == "OPERATIONS_MANAGER")))
            .Select(b => b.BranchId).ToArrayAsync(ct);
    }

    public async Task<StockBalanceResult> ReadAsync(StockBalanceQuery query, CancellationToken ct)
    {
        Validator.ValidateObject(query, new ValidationContext(query), true);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await new Users.UserEligibilityLock(db).LockAsync([access.UserId], ct);
        await session.RequireAsync(ct);
        var branches = await Branches(ct);
        if (branches.Length == 0) throw new InsightException(403, "ACCESS_DENIED", "Current inventory read permission and Storekeeper/Operations Manager role required.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        query.To ??= DateOnly.FromDateTime(now);
        query.From ??= query.To.Value.AddDays(-Math.Min(29, query.To.Value.DayNumber));
        if (query.From > query.To || query.To == DateOnly.MaxValue)
            throw new InsightException(400, "INVALID_PERIOD", "Select a valid UTC period.");
        var warehouses = await db.Warehouses.AsNoTracking().Where(w => branches.Contains(w.BranchId)
            && (w.DepartmentId == null || w.Department!.BranchId == w.BranchId)).OrderBy(w => w.WarehouseId)
            .Select(w => new StockReadOption(w.WarehouseId, w.WarehouseName)).ToArrayAsync(ct);
        var ids = warehouses.Select(w => w.Id).ToArray();
        if (query.WarehouseId is {} warehouse && !ids.Contains(warehouse))
            throw new InsightException(403, "ACCESS_DENIED", "Warehouse outside current inventory scope.");
        var selected = query.WarehouseId is {} one ? new[] { one } : ids;
        var movements = Posted().Where(m => selected.Contains(m.WarehouseId) && m.OccurredAt <= now);
        var transfers = db.StockDocumentLines.AsNoTracking().Where(l => l.Document.DocumentType == "TRANSFER"
            && (l.Document.Status == "IN_TRANSIT" || l.Document.Status == "PARTIALLY_RECEIVED")
            && l.Document.SourceWarehouseId != null && l.Document.DestinationWarehouseId != null);
        var pairs = movements.Select(m => new { m.WarehouseId, m.ItemId })
            .Union(db.StockThresholds.Where(t => selected.Contains(t.WarehouseId)).Select(t => new { t.WarehouseId, t.ItemId }))
            .Union(transfers.Where(l => selected.Contains(l.Document.SourceWarehouseId!.Value))
                .Select(l => new { WarehouseId = l.Document.SourceWarehouseId!.Value, l.ItemId }))
            .Union(transfers.Where(l => selected.Contains(l.Document.DestinationWarehouseId!.Value))
                .Select(l => new { WarehouseId = l.Document.DestinationWarehouseId!.Value, l.ItemId }));
        var population = from p in pairs join i in db.InventoryItems.AsNoTracking() on p.ItemId equals i.ItemId
            where (query.ItemId == null || i.ItemId == query.ItemId) && (query.CategoryId == null || i.CategoryId == query.CategoryId)
            select new { p.WarehouseId, p.ItemId, Item = i.ItemName, Unit = i.BaseUom.UomCode,
                Available = movements.Where(m => m.WarehouseId == p.WarehouseId && m.ItemId == p.ItemId).Sum(m => (decimal?)m.SignedBaseQty) ?? 0,
                Minimum = db.StockThresholds.Where(t => t.WarehouseId == p.WarehouseId && t.ItemId == p.ItemId).Select(t => (decimal?)t.MinimumQty).FirstOrDefault() };
        if (query.BelowMinimum) population = population.Where(p => p.Minimum != null && p.Available < p.Minimum);
        var count = await population.CountAsync(ct);
        var page = await population.OrderBy(p => p.WarehouseId).ThenBy(p => p.ItemId)
            .Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize).ToArrayAsync(ct);
        var rows = new List<StockBalanceRow>();
        foreach (var p in page)
        {
            if (await transfers.AnyAsync(l => l.ItemId == p.ItemId &&
                (l.Document.SourceWarehouseId == p.WarehouseId || l.Document.DestinationWarehouseId == p.WarehouseId)
                && l.StockTransferReceiptLines.Any(r => r.TransferReceipt.TransferDocumentId != l.DocumentId), ct))
                throw new InsightException(409, "INCONSISTENT_TRANSFER", "Transfer receipt belongs to another document.");
            var lines = await transfers.Where(l => l.ItemId == p.ItemId &&
                (l.Document.SourceWarehouseId == p.WarehouseId || l.Document.DestinationWarehouseId == p.WarehouseId))
                .Select(l => new { Source = l.Document.SourceWarehouseId, l.ActualQty, l.ConversionFactor,
                    Reconciled = l.StockTransferReceiptLines.Sum(r => (decimal?)(r.ReceivedQty + r.DamagedQty + r.MissingQty)) ?? 0 }).ToArrayAsync(ct);
            if (lines.Any(l => l.ConversionFactor <= 0 || l.Reconciled < 0 || l.Reconciled > l.ActualQty))
                throw new InsightException(409, "INCONSISTENT_TRANSFER", "Transfer quantity evidence requires repair; no estimated balance returned.");
            rows.Add(new(p.WarehouseId, p.ItemId, p.Item, p.Unit, p.Available,
                lines.Where(l => l.Source == p.WarehouseId).Sum(l => (l.ActualQty - l.Reconciled) * l.ConversionFactor),
                lines.Where(l => l.Source != p.WarehouseId).Sum(l => (l.ActualQty - l.Reconciled) * l.ConversionFactor),
                p.Minimum, p.Minimum is {} minimum && p.Available < minimum));
        }
        decimal? opening = null, closing = null;
        var ledger = new List<StockLedgerRow>();
        if (query.WarehouseId != null && query.ItemId != null)
        {
            var start = query.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var end = query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var source = movements.Where(m => m.ItemId == query.ItemId);
            opening = await source.Where(m => m.OccurredAt < start).SumAsync(m => (decimal?)m.SignedBaseQty, ct) ?? 0;
            var period = source.Where(m => m.OccurredAt >= start && m.OccurredAt < end);
            if (await period.CountAsync(ct) > 50000) throw new InsightException(400, "LEDGER_LIMIT", "Narrow the period to at most 50000 movements.");
            var raw = await period.OrderBy(m => m.OccurredAt).ThenBy(m => m.MovementId)
                .Select(m => new { m.MovementId, m.DocumentLine.DocumentId, m.DocumentLine.Document.DocumentNo,
                    m.DocumentLine.Document.DocumentType, m.OccurredAt, m.SignedBaseQty }).ToArrayAsync(ct);
            closing = opening;
            foreach (var m in raw) { closing += m.SignedBaseQty; ledger.Add(new(m.MovementId, m.DocumentId, m.DocumentNo,
                m.DocumentType, InsightAccess.Utc(m.OccurredAt), m.SignedBaseQty, closing.Value)); }
        }
        var categories = await db.ItemCategories.AsNoTracking().OrderBy(c => c.ItemCategoryId)
            .Select(c => new StockReadOption(c.ItemCategoryId, c.CategoryName)).ToArrayAsync(ct);
        if (branches.Except(await Branches(ct)).Any()) throw new InsightException(403, "ACCESS_DENIED", "Inventory authority changed.");
        await session.RequireAsync(ct);
        await tx.CommitAsync(ct);
        return new(InsightAccess.Utc(now), query, warehouses, categories, rows.ToArray(), count, opening, closing, ledger.ToArray());
    }
}
