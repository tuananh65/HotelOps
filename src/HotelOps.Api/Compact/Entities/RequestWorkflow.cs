namespace HotelOps.Api.Compact.Entities;

// Additive, normalized coordination metadata. WorkItem remains the public request identity.
public sealed class RequestWorkflow
{
    public long WorkId { get; set; }
    public long BranchId { get; set; }
    public string RequestType { get; set; } = "";
    public string Status { get; set; } = "";
    public long? QueueRoleId { get; set; }
    public long? HandlerUserId { get; set; }
    public long? AllocationId { get; set; }
    public long? WarehouseId { get; set; }
    public long? DocumentId { get; set; }
    public long? ParentRequestWorkId { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class RequestWorkflowLine
{
    public long RequestLineId { get; set; }
    public long WorkId { get; set; }
    public long ItemId { get; set; }
    public long UnitId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public decimal? ApprovedQuantity { get; set; }
    public decimal StockAtRequest { get; set; }
    public decimal MinimumAtRequest { get; set; }
    public long? DocumentLineId { get; set; }
}

public sealed class RequestWorkflowEvent
{
    public long RequestEventId { get; set; }
    public long WorkId { get; set; }
    public long BranchId { get; set; }
    public string Action { get; set; } = "";
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = "";
    public long? PreviousQueueRoleId { get; set; }
    public long? NewQueueRoleId { get; set; }
    public long? PreviousHandlerUserId { get; set; }
    public long? NewHandlerUserId { get; set; }
    public long ActorUserId { get; set; }
    public long ActorRoleId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Reason { get; set; }
    public long? RelatedDocumentId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid OperationKey { get; set; }
    public string PayloadHash { get; set; } = "";
    public byte[]? PreviousVersion { get; set; }
}
