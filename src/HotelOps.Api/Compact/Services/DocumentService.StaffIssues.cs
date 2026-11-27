using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class DocumentService
{
    private sealed class StaffIssueRow
    {
        public long Id { get; init; }
        public bool Reusable { get; init; }
        public string Kind { get; init; } = "";
        public long? DocumentId { get; init; }
        public string? DocumentNo { get; init; }
        public Guid OperationKey { get; init; }
        public long RecipientId { get; init; }
        public string Recipient { get; init; } = "";
        public long WarehouseId { get; init; }
        public string Warehouse { get; init; } = "";
        public string Item { get; init; } = "";
        public string Unit { get; init; } = "";
        public decimal Quantity { get; init; }
        public DateTime OccurredAt { get; init; }
    }

    private IQueryable<StaffIssueRow> StaffIssueRows(long branchId)
    {
        var supplies = from entry in db.StaffSupplyLedgers.AsNoTracking()
            join balance in db.StaffSupplyBalances on entry.BalanceId equals balance.BalanceId
            join allocation in db.StaffSupplyAllocations on balance.AllocationId equals allocation.AllocationId
            join user in db.Users on allocation.UserId equals user.UserId
            join item in db.InventoryItems on balance.ItemId equals item.ItemId
            join unit in db.ReferenceValues on balance.UnitId equals unit.ReferenceId
            join warehouse in db.Warehouses on balance.WarehouseId equals warehouse.WarehouseId
            join line in db.InventoryDocumentLines on entry.DocumentLineId equals line.LineId into lines
            from line in lines.DefaultIfEmpty()
            join document in db.InventoryDocuments on line.DocumentId equals document.DocumentId into documents
            from document in documents.DefaultIfEmpty()
            where allocation.BranchId == branchId && warehouse.BranchId == branchId
                && (entry.Kind == "INITIAL" || entry.Kind == "ADDITIONAL") && entry.Quantity > 0
                && (document == null || document.BranchId == branchId && document.PostedAt != null)
                && !db.ReusableMovements.Any(m => m.DocumentLineId != null && m.DocumentLineId == entry.DocumentLineId && m.Kind == "HANDOVER")
            select new StaffIssueRow { Id = entry.LedgerId, Reusable = false, Kind = entry.Kind,
                DocumentId = document == null ? null : document.DocumentId, DocumentNo = document == null ? null : document.DocumentNo,
                OperationKey = document == null ? entry.OperationKey : Guid.Empty,
                RecipientId = user.UserId, Recipient = user.FullName, WarehouseId = warehouse.WarehouseId,
                Warehouse = warehouse.Name, Item = item.Name, Unit = unit.Name, Quantity = entry.Quantity, OccurredAt = entry.OccurredAt };
        var reusable = from entry in db.ReusableMovements.AsNoTracking()
            join source in db.ReusablePositions on entry.SourcePositionId equals source.PositionId
            join destination in db.ReusablePositions on entry.DestinationPositionId equals destination.PositionId
            join user in db.Users on destination.UserId equals user.UserId
            join item in db.InventoryItems on entry.ItemId equals item.ItemId
            join unit in db.ReferenceValues on item.BaseUnitId equals unit.ReferenceId
            join warehouse in db.Warehouses on source.WarehouseId equals warehouse.WarehouseId
            join line in db.InventoryDocumentLines on entry.DocumentLineId equals line.LineId into lines
            from line in lines.DefaultIfEmpty()
            join document in db.InventoryDocuments on line.DocumentId equals document.DocumentId into documents
            from document in documents.DefaultIfEmpty()
            where entry.BranchId == branchId && source.BranchId == branchId && destination.BranchId == branchId
                && warehouse.BranchId == branchId && entry.Kind == "HANDOVER" && entry.Quantity > 0
                && (document == null || document.BranchId == branchId && document.PostedAt != null)
            select new StaffIssueRow { Id = entry.ReusableMovementId, Reusable = true, Kind = entry.Kind,
                DocumentId = document == null ? null : document.DocumentId, DocumentNo = document == null ? null : document.DocumentNo,
                OperationKey = document == null ? entry.OperationKey : Guid.Empty,
                RecipientId = user.UserId, Recipient = user.FullName, WarehouseId = warehouse.WarehouseId,
                Warehouse = warehouse.Name, Item = item.Name, Unit = unit.Name, Quantity = entry.Quantity, OccurredAt = entry.OccurredAt };
        return supplies.Concat(reusable);
    }

    private sealed class StaffIssueGroup
    {
        public long? DocumentId { get; init; }
        public string? DocumentNo { get; init; }
        public Guid OperationKey { get; init; }
        public long RecipientId { get; init; }
        public string Recipient { get; init; } = "";
        public long WarehouseId { get; init; }
        public string Warehouse { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public int ItemCount { get; init; }
        public int Matches { get; init; }
    }

    private IQueryable<StaffIssueGroup> StaffIssueGroups(long branchId, string term)
    {
        // Aggregate before filtering/paging so item search never truncates a supply document.
        return StaffIssueRows(branchId)
            .GroupBy(x => new { x.DocumentId, x.DocumentNo, x.OperationKey, x.RecipientId, x.Recipient, x.WarehouseId, x.Warehouse })
            .Select(g => new StaffIssueGroup { DocumentId = g.Key.DocumentId, DocumentNo = g.Key.DocumentNo,
                OperationKey = g.Key.OperationKey, RecipientId = g.Key.RecipientId, Recipient = g.Key.Recipient,
                WarehouseId = g.Key.WarehouseId, Warehouse = g.Key.Warehouse, CreatedAt = g.Min(x => x.OccurredAt),
                ItemCount = g.Count(), Matches = g.Max(x => x.Recipient.Contains(term) || x.Item.Contains(term)
                    || x.Warehouse.Contains(term) || (x.DocumentNo != null && x.DocumentNo.Contains(term)) ? 1 : 0) });
    }

    public async Task<object> StaffIssueDocuments(long branchId, int page, int pageSize, string? search,
        DateTime? from, DateTime? to, string sort, string? status, CancellationToken ct)
    {
        await access.Require("INVENTORY_VIEW", branchId, ct: ct);
        Output.Require(from == null || to == null || from <= to, "INVALID_DATE_RANGE", 400);
        Output.Require(sort is "latest" or "oldest" or "number", "INVALID_SORT", 400);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var term = search?.Trim() ?? "";
        var query = StaffIssueGroups(branchId, term);
        if (term.Length > 0) query = query.Where(x => x.Matches == 1);
        if (from != null) query = query.Where(x => x.CreatedAt >= from);
        if (to != null) query = query.Where(x => x.CreatedAt <= to);
        if (!string.IsNullOrEmpty(status) && status != "POSTED") query = query.Where(x => false);
        var total = await query.CountAsync(ct);
        var ordered = sort == "oldest" ? query.OrderBy(x => x.CreatedAt)
            : sort == "number" ? query.OrderBy(x => x.DocumentNo) : query.OrderByDescending(x => x.CreatedAt);
        var rows = await ordered.ThenBy(x => x.DocumentId).ThenBy(x => x.OperationKey).ThenBy(x => x.RecipientId).ThenBy(x => x.WarehouseId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new { items = rows.Select(x => new { type = "staffIssue", fields = new {
            documentId = StaffIssueKey(x.DocumentId, x.OperationKey, x.RecipientId, x.WarehouseId),
            documentNo = x.DocumentNo ?? $"Lần cấp {x.OperationKey:D}", kind = "STAFF_ISSUE", status = "POSTED",
            createdAt = x.CreatedAt, reason = $"{x.Recipient} · {x.Warehouse} · {x.ItemCount} dòng vật tư" } }), total, page, pageSize };
    }

    private static string StaffIssueKey(long? documentId, Guid operationKey, long recipientId, long warehouseId)
        => $"{(documentId is long id ? $"d{id}" : $"o{operationKey:D}")}:{recipientId}:{warehouseId}";

    public async Task<object> StaffIssueDocument(long branchId, string key, CancellationToken ct)
    {
        await access.Require("INVENTORY_VIEW", branchId, ct: ct);
        var parts = key.Split(':');
        if (parts.Length != 3 || parts[0].Length < 2 || !long.TryParse(parts[1], out var recipientId)
            || !long.TryParse(parts[2], out var warehouseId)) throw new ApiException(400, "INVALID_DOCUMENT_KEY");
        var query = StaffIssueRows(branchId).Where(x => x.RecipientId == recipientId && x.WarehouseId == warehouseId);
        if (parts[0][0] == 'd' && long.TryParse(parts[0][1..], out var documentId)) query = query.Where(x => x.DocumentId == documentId);
        else if (parts[0][0] == 'o' && Guid.TryParse(parts[0][1..], out var operationKey)) query = query.Where(x => x.DocumentId == null && x.OperationKey == operationKey);
        else throw new ApiException(400, "INVALID_DOCUMENT_KEY");
        var rows = await query.OrderBy(x => x.OccurredAt).ThenBy(x => x.Reusable).ThenBy(x => x.Id).ToArrayAsync(ct);
        if (rows.Length == 0) throw new ApiException(404, "DOCUMENT_NOT_FOUND");
        var first = rows[0];
        return new { documentNo = first.DocumentNo ?? $"Lần cấp {first.OperationKey:D}", first.Recipient, first.Warehouse,
            occurredAt = first.OccurredAt, sourceDocumentId = first.DocumentId, items = rows.Select(x => new {
                x.Id, x.Reusable, x.Kind, x.Item, x.Unit, x.Quantity, x.OccurredAt }) };
    }

    public async Task<object> StaffIssues(long branchId, int page, int pageSize, string? search,
        DateTime? from, DateTime? to, CancellationToken ct)
    {
        await access.Require("INVENTORY_VIEW", branchId, ct: ct);
        Output.Require(from == null || to == null || from <= to, "INVALID_DATE_RANGE", 400);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        // Read immutable issue events, not current balances or requested quantities.
        var supplies = from entry in db.StaffSupplyLedgers.AsNoTracking()
            join balance in db.StaffSupplyBalances on entry.BalanceId equals balance.BalanceId
            join allocation in db.StaffSupplyAllocations on balance.AllocationId equals allocation.AllocationId
            join user in db.Users on allocation.UserId equals user.UserId
            join item in db.InventoryItems on balance.ItemId equals item.ItemId
            join unit in db.ReferenceValues on balance.UnitId equals unit.ReferenceId
            join warehouse in db.Warehouses on balance.WarehouseId equals warehouse.WarehouseId
            where allocation.BranchId == branchId && (entry.Kind == "INITIAL" || entry.Kind == "ADDITIONAL") && entry.Quantity > 0
                && !db.ReusableMovements.Any(m => m.DocumentLineId != null && m.DocumentLineId == entry.DocumentLineId && m.Kind == "HANDOVER")
            select new { Id = entry.LedgerId, Reusable = false, entry.Kind, entry.OccurredAt,
                Recipient = user.FullName, Item = item.Name, Unit = unit.Name, Warehouse = warehouse.Name, entry.Quantity };
        var reusable = from entry in db.ReusableMovements.AsNoTracking()
            join source in db.ReusablePositions on entry.SourcePositionId equals source.PositionId
            join destination in db.ReusablePositions on entry.DestinationPositionId equals destination.PositionId
            join user in db.Users on destination.UserId equals user.UserId
            join item in db.InventoryItems on entry.ItemId equals item.ItemId
            join unit in db.ReferenceValues on item.BaseUnitId equals unit.ReferenceId
            join warehouse in db.Warehouses on source.WarehouseId equals warehouse.WarehouseId
            where entry.BranchId == branchId && source.BranchId == branchId && destination.BranchId == branchId
                && entry.Kind == "HANDOVER" && entry.Quantity > 0
            select new { Id = entry.ReusableMovementId, Reusable = true, entry.Kind, entry.OccurredAt,
                Recipient = user.FullName, Item = item.Name, Unit = unit.Name, Warehouse = warehouse.Name, entry.Quantity };
        var query = supplies.Concat(reusable);
        if (!string.IsNullOrWhiteSpace(search)) {
            var term = search.Trim();
            query = query.Where(x => x.Recipient.Contains(term) || x.Item.Contains(term) || x.Warehouse.Contains(term));
        }
        if (from != null) query = query.Where(x => x.OccurredAt >= from);
        if (to != null) query = query.Where(x => x.OccurredAt <= to);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.OccurredAt).ThenBy(x => x.Reusable).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new { items, total, page, pageSize };
    }
}
