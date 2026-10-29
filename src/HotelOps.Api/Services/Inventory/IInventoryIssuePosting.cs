using HotelOps.Api.DTOs.Inventory;

namespace HotelOps.Api.Services.Inventory;

public record InventoryIssueLine(long RequestLineId, long ItemId, long UomId, decimal Quantity);
public record InventoryIssueRequest(long ActorUserId, long FulfillmentId, long WarehouseId,
    DateOnly DocumentDate, IReadOnlyList<InventoryIssueLine> Lines);
public record InventoryIssueCorrelation(long RequestLineId, long StockDocumentLineId);
public record InventoryIssueResult(long DocumentId, IReadOnlyList<InventoryIssueCorrelation> Lines);

/// <summary>
/// FE-09 must authorize/validate fulfillment, begin a transaction on the SAME scoped
/// HotelOpsDbContext, call this primitive before acquiring warehouse/stock locks,
/// update fulfillment totals/linkage and commit atomically. This primitive never commits.
/// The caller must roll back and discard tracked state after any failure.
/// ActorUserId is supplied by the trusted calling business service, never model-bound from an Inventory HTTP request.
/// </summary>
public interface IInventoryIssuePosting
{
    Task<InventoryIssueResult> PostIssue(InventoryIssueRequest request, CancellationToken cancellationToken);
}
