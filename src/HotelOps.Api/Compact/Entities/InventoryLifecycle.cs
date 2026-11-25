namespace HotelOps.Api.Compact.Entities;

public sealed class ItemLifecycleProfile
{
    public long BranchId { get; set; }
    public long ItemId { get; set; }
    public string Classification { get; set; } = "UNCLASSIFIED";
    public string ExpiryPolicy { get; set; } = "UNKNOWN";
    public DateTime UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class StockBatch
{
    public long BatchId { get; set; }
    public long BranchId { get; set; }
    public long WarehouseId { get; set; }
    public long ItemId { get; set; }
    public string LotNumber { get; set; } = "";
    public DateTime? ReceivedAt { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal WarehouseQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public string Status { get; set; } = "LEGACY_REVIEW";
    public long? ReceiptLineId { get; set; }
    public long? OriginBatchId { get; set; }
    public bool IsLegacy { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class StockBatchEntry
{
    public long EntryId { get; set; }
    public long BatchId { get; set; }
    public long? MovementId { get; set; }
    public long? LedgerId { get; set; }
    public string Kind { get; set; } = "WAREHOUSE";
    public decimal Quantity { get; set; }
    public DateTime OccurredAt { get; set; }
}
public sealed class StaffBatchBalance
{
    public long StaffBatchBalanceId { get; set; }
    public long BalanceId { get; set; }
    public long BatchId { get; set; }
    public decimal Quantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
