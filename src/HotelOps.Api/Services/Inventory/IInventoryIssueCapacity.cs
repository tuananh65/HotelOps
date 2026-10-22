namespace HotelOps.Api.Services.Inventory;

public sealed record InventoryIssueCapacity(long StockDocumentLineId, decimal IssuedBaseQty, decimal ConsumedBaseQty, decimal ReturnedBaseQty)
{
    public decimal RemainingBaseQty => IssuedBaseQty - ConsumedBaseQty - ReturnedBaseQty;
}

// Same scoped DbContext/transaction as caller. Never begins/commits/rolls back.
public interface IInventoryIssueCapacity
{
    Task AcquireCatalog(CancellationToken ct);
    Task LockSourceDocuments(IReadOnlyCollection<long> lineIds, CancellationToken ct);
    Task<InventoryIssueCapacity> Capacity(long issueLineId, long? excludeReturnDocument, CancellationToken ct);
    Task<InventoryIssueCapacity> ValidateCapacity(long issueLineId, decimal additionalConsumed, decimal additionalReturned, long? excludeReturnDocument, CancellationToken ct);
    decimal HistoricalBase(decimal quantity, decimal factor);
}
