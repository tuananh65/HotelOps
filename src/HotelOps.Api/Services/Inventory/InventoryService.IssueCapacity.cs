using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

public sealed partial class InventoryService : IInventoryIssueCapacity
{
    public Task AcquireCatalog(CancellationToken ct) => Gate("HotelOps:Inventory:Catalog", ct);
    private async Task RequireCapacityTransaction(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Issue capacity requires caller transaction.");
        if (await db.Database.SqlQueryRaw<string>("SELECT APPLOCK_MODE('public','HotelOps:Inventory:Catalog','Transaction') AS Value").SingleAsync(ct) != "Exclusive")
            throw new InvalidOperationException("Acquire Inventory Catalog before owner locks; capacity does not acquire it late.");
    }
    public async Task LockSourceDocuments(IReadOnlyCollection<long> lineIds, CancellationToken ct)
    {
        await RequireCapacityTransaction(ct);
        var ids = lineIds.Distinct().ToArray();
        var documents = await db.StockDocumentLines.Where(x => ids.Contains(x.LineId)).Select(x => x.DocumentId).ToListAsync(ct);
        if (documents.Count != ids.Length) throw Invalid("Referenced ISSUE line does not exist.");
        foreach (var id in documents.Distinct().Order()) await Document(id, ct);
    }
    public decimal HistoricalBase(decimal quantity, decimal factor)
    {
        if (Qty(quantity) <= 0 || factor <= 0 || factor > 999999999999.999999m || decimal.Round(factor, 6) != factor) throw Invalid("Positive exact historical quantity/factor required.");
        return Base(quantity, factor);
    }
    public async Task<InventoryIssueCapacity> Capacity(long issueLineId, long? excludeReturnDocument, CancellationToken ct)
    {
        await RequireCapacityTransaction(ct);
        var issued = -(await db.StockMovements.Where(x => x.DocumentLineId == issueLineId && x.SignedBaseQty < 0).SumAsync(x => (decimal?)x.SignedBaseQty, ct) ?? 0);
        var consumed = await db.MaintenanceMaterialUsages.Where(x => x.StockDocumentLineId == issueLineId).SumAsync(x => (decimal?)x.UsedBaseQty, ct) ?? 0;
        var returned = await db.StockReturnLineDetails.Where(x => x.OriginalIssueLineId == issueLineId && x.StockDocumentLine.DocumentId != excludeReturnDocument && x.StockDocumentLine.Document.Status == "POSTED")
            .SumAsync(x => (decimal?)((x.StockDocumentLine.ActualQty + x.DamagedQty) * x.StockDocumentLine.ConversionFactor), ct) ?? 0;
        if (issued < 0 || consumed < 0 || returned < 0 || consumed + returned > issued) throw Invalid("Persisted ISSUE consumption/return capacity is inconsistent.");
        return new(issueLineId, issued, consumed, returned);
    }
    public async Task<InventoryIssueCapacity> ValidateCapacity(long issueLineId, decimal additionalConsumed, decimal additionalReturned, long? excludeReturnDocument, CancellationToken ct)
    {
        var before = await Capacity(issueLineId, excludeReturnDocument, ct);
        Qty(additionalConsumed); Qty(additionalReturned);
        if (before.ConsumedBaseQty + additionalConsumed + before.ReturnedBaseQty + additionalReturned > before.IssuedBaseQty)
            throw Invalid("Consumed plus physically returned quantity exceeds the original ISSUE quantity.");
        return before;
    }
}
