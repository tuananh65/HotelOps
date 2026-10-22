namespace HotelOps.Api.DTOs.Inventory;

public record InventoryPage(int Page = 1, int PageSize = 50);
public record InventoryPaged<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public record InventoryCommand(long ActorUserId, string ExpectedRowVersion, string? Reason = null);
public record InventoryDraftLine(long ItemId, long UomId, decimal ExpectedQty, long? OriginalIssueLineId = null);
public record InventoryDraft(long ActorUserId, long? SourceWarehouseId, long? DestinationWarehouseId,
    DateOnly DocumentDate, IReadOnlyList<InventoryDraftLine> Lines, string? Reason = null, string? Note = null,
    string? ExpectedRowVersion = null);
public record InventoryAcceptedLine(long LineId, decimal AcceptedQty, decimal RejectedQty = 0,
    decimal DamagedQty = 0, string? DiscrepancyReason = null);
public record InventoryPost(long ActorUserId, string ExpectedRowVersion, IReadOnlyList<InventoryAcceptedLine> Lines);
public record InventoryReceiveLine(long TransferLineId, decimal ReceivedQty, decimal DamagedQty,
    decimal MissingQty, string? Note = null);
public record InventoryReceive(long ActorUserId, string ExpectedRowVersion,
    IReadOnlyList<InventoryReceiveLine> Lines, string? Note = null);
public record InventoryDelta(long ItemId, decimal DeltaBaseQty);
public record InventoryAdjustment(long ActorUserId, long WarehouseId, DateOnly DocumentDate,
    string Reason, IReadOnlyList<InventoryDelta> Lines);
public record InventoryItemWrite(long ActorUserId, string ItemCode, string ItemName, long CategoryId,
    long BaseUomId, bool IsConsumable, bool IsActive = true, string? Description = null,
    string? Specification = null, string? ExpectedRowVersion = null);
public record InventoryUnitWrite(long ActorUserId, string UomCode, string UomName, bool IsActive = true,
    string? Description = null, string? ExpectedRowVersion = null);
public record InventoryConversionWrite(long ActorUserId, long FromUomId, decimal ConversionFactor,
    bool IsActive = true, string? ExpectedRowVersion = null);
public record InventoryWarehouseWrite(long ActorUserId, long BranchId, long? DepartmentId,
    string WarehouseCode, string WarehouseName, bool IsActive = true, long? ManagerUserId = null,
    string? LocationDescription = null, string? ExpectedRowVersion = null);
public record InventoryThresholdWrite(long ActorUserId, decimal MinimumQty, string? ExpectedRowVersion = null);
public record InventoryItemDto(long ItemId, string ItemCode, string ItemName, long CategoryId,
    long BaseUomId, bool IsActive, bool IsConsumable, string? Description, string? Specification, string RowVersion);
public record InventoryUnitDto(long UomId, string UomCode, string UomName, bool IsActive, string? Description, string RowVersion);
public record InventoryConversionDto(long ConversionId, long? ItemId, long FromUomId, long ToUomId,
    decimal ConversionFactor, bool IsActive, string RowVersion);
public record InventoryWarehouseDto(long WarehouseId, long BranchId, long? DepartmentId, string WarehouseCode,
    string WarehouseName, long? ManagerUserId, string? LocationDescription, bool IsActive, string RowVersion);
public record InventoryThresholdDto(long ThresholdId, long WarehouseId, long ItemId, decimal MinimumQty,
    decimal? MaximumQty, decimal? ReorderQty, bool AlertEnabled, string RowVersion);
public record InventoryLookup(long Id, string Code, string Name, bool IsActive);
public record InventoryStockDto(long WarehouseId, long ItemId, decimal OnHand, decimal Available,
    decimal? MinimumQty, bool BelowMinimum)
{
    public InventoryStockDto() : this(0, 0, 0, 0, null, false) { }
}
public record InventoryMovementDto(long MovementId, long DocumentLineId, long DocumentId, string DocumentNo,
    string DocumentType, string Status, long WarehouseId, long ItemId, decimal SignedBaseQty, DateTime OccurredAt)
{
    public InventoryMovementDto() : this(0, 0, 0, "", "", "", 0, 0, 0, default) { }
}
public record InventoryLedger(decimal OpeningBalance, IReadOnlyList<InventoryMovementDto> Movements, decimal ClosingBalance);
public record InventoryLineDto(long LineId, int LineNo, long ItemId, long UomId, decimal ConversionFactor,
    decimal? RequestedQty, decimal ActualQty, decimal BaseQty, decimal? RejectedQty, decimal? DamagedQty,
    string? DiscrepancyReason, long? OriginalIssueLineId, decimal? ReceivedQty, decimal? MissingQty, decimal? InTransitQty);
public record InventoryBatchLineDto(long TransferLineId, decimal ReceivedQty, decimal DamagedQty, decimal MissingQty, string? Note);
public record InventoryBatchDto(long TransferReceiptId, long ReceivedBy, DateTime ReceivedAt, string? Note,
    IReadOnlyList<InventoryBatchLineDto> Lines);
public record InventoryDocumentDto(long DocumentId, string DocumentNo, string DocumentType, string Status,
    long? SourceWarehouseId, long? DestinationWarehouseId, DateOnly DocumentDate, string? Reason, string? Note,
    DateTime CreatedAt, DateTime? PostedAt, DateTime? CancelledAt, long? ReversalOfDocumentId, string RowVersion,
    IReadOnlyList<InventoryLineDto> Lines, IReadOnlyList<InventoryBatchDto> ReceivingBatches);
public record InventoryHistoryDto(long AuditId, long? BranchId, long? ActorUserId, string Action,
    DateTime OccurredAt, string? OldValues, string? NewValues, string? ChangedFields);
public record InventoryStocktakeCreate(long ActorUserId, long WarehouseId, string? Note = null);
public record InventoryCount(long StocktakeLineId, decimal? CountedQty, string? DiscrepancyReason = null);
public record InventoryCounts(long ActorUserId, string ExpectedRowVersion, IReadOnlyList<InventoryCount> Lines);
public record InventoryStocktakeLineDto(long StocktakeLineId, long ItemId, decimal SystemQty,
    decimal? CountedQty, decimal? VarianceQty, string? DiscrepancyReason, long? CountedBy, DateTime? CountedAt);
public record InventoryStocktakeDto(long StocktakeId, string StocktakeNo, long WarehouseId, DateTime SnapshotAt,
    string Status, long? CountedBy, long? ApprovedBy, long? AdjustmentDocumentId, string? Note,
    string RowVersion, IReadOnlyList<InventoryStocktakeLineDto> Lines);
