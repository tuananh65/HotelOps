using HotelOps.Api.DTOs.Inventory;

namespace HotelOps.Api.DTOs.Replenishments;

public sealed record ReplenishmentLineWrite(int LineNo, long ItemId, string SourceType, long? SourceThresholdId,
    long? SourceMaterialRequestLineId, decimal RequestedBaseQty, string? Note = null);
public sealed record ReplenishmentCreate(long ActorUserId, long WarehouseId, string? SupplierName, DateOnly? ExpectedOn, IReadOnlyList<ReplenishmentLineWrite> Lines);
public sealed record ReplenishmentClose(long ActorUserId, string ExpectedRowVersion, string Reason);
public sealed record ReplenishmentReceiptMapping(long StockDocumentLineId, long ReplenishmentLineId);
public sealed record ReplenishmentLink(long ActorUserId, string ExpectedRowVersion, IReadOnlyList<ReplenishmentReceiptMapping> Lines);
public sealed record ReplenishmentPost(long ActorUserId, string ExpectedRowVersion, long ReceiptDocumentId,
    string ReceiptExpectedRowVersion, IReadOnlyList<InventoryAcceptedLine> ReceiptLines, IReadOnlyList<ReplenishmentReceiptMapping> Lines);
public sealed class ReplenishmentPage
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? BranchId { get; set; }
    public long? WarehouseId { get; set; }
    public long? ItemId { get; set; }
    public string? Status { get; set; }
    public string? SourceType { get; set; }
    public string? Supplier { get; set; }
    public string? Keyword { get; set; }
    public DateOnly? ExpectedFrom { get; set; }
    public DateOnly? ExpectedTo { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
}
public sealed record ReplenishmentPaged<T>(int Page, int PageSize, int Total, IReadOnlyList<T> Items);
public sealed record ReplenishmentReceiptDto(long LinkId, long ReplenishmentLineId, long StockDocumentId, string DocumentNo,
    long StockDocumentLineId, decimal CreditedBaseQty, decimal EffectiveBaseQty, string ReceiptStatus, long? ReversalDocumentId,
    DateTime LinkedAt, long LinkedBy);
public sealed record ReplenishmentLineDto(long ReplenishmentLineId, int LineNo, long ItemId, long BaseUomId, string SourceType,
    long? SourceThresholdId, long? SourceMaterialRequestLineId, long? SourceMaterialRequestId, decimal RequestedBaseQty,
    decimal GrossCreditedBaseQty, decimal NetReceivedBaseQty, decimal RemainingBaseQty, bool IsOpenCoverage, string? Note);
public sealed record ReplenishmentDto(long ReplenishmentId, string ReplenishmentNo, string RawStatus, string RowVersion,
    long WarehouseId, long BranchId, long RequestedBy, string? SupplierName, DateOnly? ExpectedOn, DateTime CreatedAt,
    DateTime? UpdatedAt, DateTime? ClosedAt, long? ClosedBy, string? CloseReason, IReadOnlyList<ReplenishmentLineDto> Lines,
    IReadOnlyList<ReplenishmentReceiptDto> Receipts);
public sealed record ReplenishmentSummary(long ReplenishmentId, string ReplenishmentNo, long WarehouseId, long BranchId,
    string Status, string? SupplierName, DateOnly? ExpectedOn, DateTime CreatedAt);
public sealed record ReplenishmentAuditDto(long AuditId, string EntityType, string EntityId, string Action, long? ActorUserId,
    long? BranchId, DateTime OccurredAt, string? OldValues, string? NewValues, string? ChangedFields);
public sealed record ReplenishmentHistory(ReplenishmentDto Replenishment, IReadOnlyList<ReplenishmentAuditDto> Audit);
public sealed record LowStockShortage(long WarehouseId, long BranchId, long ItemId, long BaseUomId, decimal OnHand,
    decimal MinimumQty, decimal DeficitToMinimum, long ThresholdId, long? ExistingActiveReplenishmentId, string? ExistingCoverageStatus);
public sealed record MaterialRequestShortage(long RequestId, string RequestNo, long RequestLineId, long BranchId, long WarehouseId,
    long ItemId, long RequestUomId, long BaseUomId, decimal ApprovedQty, decimal FulfilledQty, decimal RemainingRequestQty,
    decimal RemainingBaseQty, decimal CurrentOnHand, decimal AggregateApprovedDemandBaseQty, decimal PhysicalShortageBaseQty,
    long? ExistingActiveReplenishmentId);
