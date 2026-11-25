namespace HotelOps.Api.Compact.Entities;

// Database-first contract for p0-supplies.sql. Configuration revisions are immutable.
public sealed class SupplyConfiguration
{
    public long ConfigurationId { get; set; }
    public long BranchId { get; set; }
    public long? ItemId { get; set; }
    public long? AssetId { get; set; }
    public string SupplyGroup { get; set; } = "A";
    public long? WarehouseId { get; set; }
    public long? UnitId { get; set; }
    public decimal InitialQuantity { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal PerGuestQuantity { get; set; }
    public bool IsActive { get; set; }
    public int Revision { get; set; }
    public bool IsCurrent { get; set; }
    public long UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class StaffSupplyAllocation
{
    public long AllocationId { get; set; }
    public long BranchId { get; set; }
    public long UserId { get; set; }
    public long AssignmentId { get; set; }
    public string Status { get; set; } = "SHORT";
    public string? ShortageReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class StaffSupplyBalance
{
    public long BalanceId { get; set; }
    public long AllocationId { get; set; }
    public long ConfigurationId { get; set; }
    public long ItemId { get; set; }
    public long WarehouseId { get; set; }
    public long UnitId { get; set; }
    public decimal InitialTarget { get; set; }
    public decimal PerGuestQuantity { get; set; }
    public decimal InitialIssued { get; set; }
    public decimal AdditionalIssued { get; set; }
    public decimal Reserved { get; set; }
    public decimal Used { get; set; }
    public decimal Returned { get; set; }
    public decimal ReclassifiedQuantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class SupplyReservation
{
    public long ReservationId { get; set; }
    public long BalanceId { get; set; }
    public long WorkId { get; set; }
    public long RoomId { get; set; }
    public int Capacity { get; set; }
    public decimal PerGuestQuantity { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UsableQuantity { get; set; }
    public DateTime? AssessedAt { get; set; }
    public long? AssessedBy { get; set; }
    public string Status { get; set; } = "RESERVED";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class StaffSupplyLedger
{
    public long LedgerId { get; set; }
    public long BalanceId { get; set; }
    public string Kind { get; set; } = "";
    public decimal Quantity { get; set; }
    public long? WorkId { get; set; }
    public long? ReservationId { get; set; }
    public long? DocumentLineId { get; set; }
    public Guid OperationKey { get; set; }
    public long ActorUserId { get; set; }
    public DateTime OccurredAt { get; set; }
}
public sealed class SupplyStockDocument
{
    public long DocumentId { get; set; }
    public long AllocationId { get; set; }
    public long WarehouseId { get; set; }
    public string Purpose { get; set; } = "INITIAL";
    public Guid OperationKey { get; set; }
    public long? RequestWorkId { get; set; }
}
