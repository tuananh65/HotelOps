using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed class InventoryAccessException(int status) : Exception("Inventory access denied.")
{
    public int Status { get; } = status;
}

// Inventory has no approved OWN rule. Branch/global decisions use the existing
// engine; no global resolver is replaced and no role name grants access.
public sealed class InventoryAccess(HotelOpsDbContext db, IPermissionService permissions)
{
    private static void Require(PermissionEvaluation result)
    {
        if (!result.IsGranted) throw new InventoryAccessException(result.Decision == PermissionDecision.Unauthenticated ? 401 : 403);
    }
    private async Task Branch(long branch, string permission, CancellationToken ct) =>
        Require(await permissions.EvaluateBranchAsync(permission, branch, ct));

    public async Task<long[]> ReadWarehouses(CancellationToken ct)
    {
        var branches = await db.Branches.AsNoTracking().Where(x => x.IsActive).Select(x => x.BranchId).ToListAsync(ct);
        var allowed = new List<long>();
        foreach (var branch in branches)
            if ((await permissions.EvaluateBranchAsync("INVENTORY_VIEW", branch, ct)).IsGranted) allowed.Add(branch);
        if (allowed.Count == 0) throw new InventoryAccessException(403);
        return await db.Warehouses.AsNoTracking().Where(x => allowed.Contains(x.BranchId)).Select(x => x.WarehouseId).ToArrayAsync(ct);
    }
    private async Task<bool> Warehouse(long? id, string permission, CancellationToken ct)
    {
        if (id is null) throw new InventoryAccessException(403);
        var branch = await db.Warehouses.AsNoTracking().Where(x => x.WarehouseId == id).Select(x => (long?)x.BranchId).SingleOrDefaultAsync(ct);
        if (branch is null) return false;
        await Branch(branch.Value, permission, ct);
        return true;
    }
    private static string DocumentPermission(string type) => type switch
    {
        "RECEIPT" => "STOCK_RECEIPT_POST", "RETURN" => "STOCK_RETURN_POST", "TRANSFER" => "STOCK_TRANSFER_POST",
        _ => throw new InventoryAccessException(403)
    };
    private async Task<bool> Document(long id, string? permission, string? side, CancellationToken ct)
    {
        var d = await db.StockDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.DocumentId == id, ct);
        if (d is null) return false;
        permission ??= DocumentPermission(d.DocumentType);
        var warehouses = side == "source" ? new[] { d.SourceWarehouseId } : side == "destination" ? new[] { d.DestinationWarehouseId }
            : new[] { d.SourceWarehouseId, d.DestinationWarehouseId }.Where(x => x.HasValue).ToArray();
        if (warehouses.Length == 0) throw new InventoryAccessException(403);
        foreach (var warehouse in warehouses.Distinct())
            if (!await Warehouse(warehouse, permission, ct)) throw new InventoryAccessException(403);
        if (permission == "STOCK_REVERSE")
            foreach (var warehouse in await db.StockMovements.AsNoTracking().Where(x => x.DocumentLine.DocumentId == id).Select(x => x.WarehouseId).Distinct().ToListAsync(ct))
                if (!await Warehouse(warehouse, permission, ct)) throw new InventoryAccessException(403);
        return true;
    }
    public async Task<bool> Authorize(string action, RouteValueDictionary route, object? input, CancellationToken ct)
    {
        long Id(string key = "id") => route.TryGetValue(key, out var value) && long.TryParse(value?.ToString(), out var id) ? id : 0;
        if (input is InventoryItemWrite or InventoryUnitWrite or InventoryConversionWrite)
        {
            Require(await permissions.EvaluateGlobalAsync("ITEM_MASTER_MANAGE", ct));
            return true;
        }
        if (input is InventoryWarehouseWrite warehouse)
        {
            if (Id() > 0 && !await Warehouse(Id(), "ITEM_MASTER_MANAGE", ct)) return false;
            var branch = await db.Branches.AsNoTracking().Where(x => x.BranchId == warehouse.BranchId && x.IsActive).Select(x => (long?)x.BranchId).SingleOrDefaultAsync(ct);
            if (branch is null) throw new InventoryAccessException(403);
            await Branch(branch.Value, "ITEM_MASTER_MANAGE", ct);
            // Department matching remains the existing business validation (400).
            return true;
        }
        if (input is InventoryThresholdWrite) return await Warehouse(Id("warehouseId"), "ITEM_MASTER_MANAGE", ct);
        if (input is InventoryDraft draft)
        {
            var permission = action.Contains("Receipt", StringComparison.Ordinal) ? "STOCK_RECEIPT_POST"
                : action.Contains("Return", StringComparison.Ordinal) ? "STOCK_RETURN_POST" : "STOCK_TRANSFER_POST";
            if (Id() > 0 && !await Document(Id(), permission, null, ct)) return false;
            var ids = new[] { draft.SourceWarehouseId, draft.DestinationWarehouseId }.Where(x => x.HasValue).ToArray();
            if (ids.Length == 0) throw new InventoryAccessException(403);
            foreach (var id in ids.Distinct()) if (!await Warehouse(id, permission, ct)) return false;
            return true;
        }
        if (input is InventoryAdjustment adjustment) return await Warehouse(adjustment.WarehouseId, "STOCK_ADJUST_POST", ct);
        if (input is InventoryStocktakeCreate stocktake) return await Warehouse(stocktake.WarehouseId, "STOCKTAKE_PERFORM", ct);
        if (action is "Counts" or "Submit" or "Reconcile" or "ApproveAdjustment" or "ReturnStocktake" or "CancelStocktake" or "Stocktake")
        {
            var stocktakeId = Id();
            var stocktakeWarehouse = await db.Stocktakes.AsNoTracking().Where(x => x.StocktakeId == stocktakeId).Select(x => (long?)x.WarehouseId).SingleOrDefaultAsync(ct);
            if (stocktakeWarehouse is null) return false;
            return await Warehouse(stocktakeWarehouse, action == "Stocktake" ? "INVENTORY_VIEW" : action == "ApproveAdjustment" ? "STOCK_ADJUST_POST" : "STOCKTAKE_PERFORM", ct);
        }
        if (action == "PostReversal")
        {
            var reversalId = Id();
            var original = await db.StockDocuments.AsNoTracking().Where(x => x.DocumentId == reversalId && x.DocumentType == "REVERSAL")
                .Select(x => x.ReversalOfDocumentId).SingleOrDefaultAsync(ct);
            if (original is null) return false;
            return await Document(original.Value, "STOCK_REVERSE", null, ct);
        }
        if (action is "PostAdjustment" or "PostReceipt" or "PostReturn" or "Cancel" or "Reverse" or "Dispatch" or "Receive" or "Document" or "History")
            return await Document(Id(), action switch
            {
                "PostAdjustment" => "STOCK_ADJUST_POST", "PostReceipt" => "STOCK_RECEIPT_POST", "PostReturn" => "STOCK_RETURN_POST", "Reverse" => "STOCK_REVERSE",
                "Dispatch" or "Receive" => "STOCK_TRANSFER_POST", "Document" or "History" => "INVENTORY_VIEW", _ => null
            }, action == "Dispatch" ? "source" : action == "Receive" ? "destination" : null, ct);
        if (action == "Warehouse") return await Warehouse(Id(), "INVENTORY_VIEW", ct);
        if (action is "Thresholds" or "StockPair" or "Ledger") return await Warehouse(Id("warehouseId"), "INVENTORY_VIEW", ct);
        // Shared catalogue reads require a legitimate Inventory read grant in an
        // active branch; all branch-bearing lists are filtered before pagination.
        if (action is not ("Items" or "Item" or "Categories" or "Units" or "Unit" or "Conversions" or "Conversion" or "Warehouses" or "Stock" or "LowStock" or "Movements" or "Documents" or "Stocktakes")) throw new InventoryAccessException(403);
        _ = await ReadWarehouses(ct);
        return true;
    }
}
