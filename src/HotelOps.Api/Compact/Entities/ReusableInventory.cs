namespace HotelOps.Api.Compact.Entities;
public sealed class ReusablePosition
{
    public long PositionId { get; set; }
    public long BranchId { get; set; }
    public long ItemId { get; set; }
    public long? WarehouseId { get; set; }
    public long? UserId { get; set; }
    public long? RoomId { get; set; }
    public string LocationKey { get; set; } = "";
    public string Status { get; set; } = "READY";
    public decimal Quantity { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class ReusableMovement
{
    public long ReusableMovementId { get; set; }
    public long BranchId { get; set; }
    public long ItemId { get; set; }
    public long? SourcePositionId { get; set; }
    public long? DestinationPositionId { get; set; }
    public decimal Quantity { get; set; }
    public string Kind { get; set; } = "MOVE";
    public long? DocumentLineId { get; set; }
    public long? WorkId { get; set; }
    public long ActorUserId { get; set; }
    public Guid OperationKey { get; set; }
    public string Reason { get; set; } = "";
    public DateTime OccurredAt { get; set; }
}
public sealed class ReusableWriteOff
{
    public long WriteOffId { get; set; }
    public long BranchId { get; set; }
    public long PositionId { get; set; }
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public Guid OperationKey { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
