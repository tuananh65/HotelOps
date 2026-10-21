namespace HotelOps.Api.DTOs.MaterialRequests;

public sealed record MaterialLineWrite(long? RequestLineId, int LineNo, long ItemId, long UomId, decimal RequestedQty, string? Note);
public sealed record MaterialRequestWrite(long ActorUserId, long BranchId, long DepartmentId, long? SourceWarehouseId,
    string Purpose, DateTime? NeededAt, long? PriorityId, long? ReceivingRoomId, long? ReceivingAreaId, long? IncidentId,
    IReadOnlyList<MaterialLineWrite> Lines, bool Submit = false, long? AssignedApproverUserId = null, string? ExpectedRowVersion = null);
public sealed record MaterialCommand(long ActorUserId, string ExpectedRowVersion, string? Reason = null, long? AssignedApproverUserId = null);
public sealed record MaterialApprovalLine(long RequestLineId, decimal ApprovedQty);
public sealed record MaterialApproval(long ActorUserId, string ExpectedRowVersion, IReadOnlyList<MaterialApprovalLine> Lines, string? Reason = null);
public sealed record MaterialIssueLine(long RequestLineId, decimal Quantity);
public sealed record MaterialIssue(long ActorUserId, string ExpectedRowVersion, string Mode, DateOnly DocumentDate, IReadOnlyList<MaterialIssueLine>? Lines = null);
public sealed record MaterialLineDto(long RequestLineId, int LineNo, long ItemId, long UomId, decimal RequestedQty,
    decimal? ApprovedQty, decimal FulfilledQty, decimal? HistoricalRemainingQty, decimal OpenFulfillableQty, bool IsActive, string? Note);
public sealed record MaterialCycleLineDto(long RequestLineId, int LineNo, long ItemId, long UomId, decimal RequestedQty, decimal? ApprovedQty, string? Note);
public sealed record MaterialDecisionDto(long ApprovalId, long? ApprovalCycleId, long ApproverId, string Decision, string? Note, DateTime DecidedAt);
public sealed record MaterialCycleDto(long ApprovalCycleId, int RevisionNo, long SubmittedBy, long AssignedApproverUserId,
    string Status, DateTime SubmittedAt, DateTime? ResolvedAt, IReadOnlyList<MaterialCycleLineDto> Lines);
public sealed record MaterialFulfillmentLineDto(long FulfillmentLineId, long RequestLineId, long ItemId, long UomId, decimal IssuedQty, decimal? ReceivedQty, long? StockDocumentLineId);
public sealed record MaterialFulfillmentDto(long FulfillmentId, string FulfillmentNo, long RequestId, long WarehouseId,
    long FulfilledBy, long? ReceivedBy, string Status, DateTime? FulfilledAt, DateTime? ReceivedAt, long? StockDocumentId,
    string? StockDocumentNo, IReadOnlyList<MaterialFulfillmentLineDto> Lines);
public sealed record MaterialReturnDto(long DocumentId, string DocumentNo, string Status, long OriginalIssueLineId);
public sealed record MaterialRequestDto(long RequestId, string RequestNo, string RawStatus, bool IsNewWorkflow, bool MutationEligible,
    string? MutationRestriction, string RowVersion, long BranchId, long DepartmentId, long RequesterId, long? SourceWarehouseId,
    string Purpose, DateTime? NeededAt, long? PriorityId, long? ReceivingRoomId, long? ReceivingAreaId, long? IncidentId,
    DateTime CreatedAt, DateTime? SubmittedAt, DateTime? CompletedAt, DateTime? UpdatedAt, string? CancellationReason,
    DateTime? RemainderClosedAt, long? RemainderClosedBy, string? RemainderCloseReason,
    IReadOnlyList<MaterialLineDto> Lines, IReadOnlyList<MaterialCycleDto> Cycles, IReadOnlyList<MaterialDecisionDto> Decisions,
    IReadOnlyList<MaterialFulfillmentDto> Fulfillments, IReadOnlyList<MaterialReturnDto> Returns);
public sealed record MaterialAuditDto(long AuditId, string EntityType, string EntityId, string Action, long? ActorUserId,
    DateTime OccurredAt, string? OldValues, string? NewValues, string? ChangedFields);
public sealed record MaterialHistoryDto(MaterialRequestDto Request, IReadOnlyList<MaterialAuditDto> Audit);
public sealed record MaterialSummaryDto(long RequestId, string RequestNo, string RawStatus, long BranchId, long DepartmentId, long RequesterId, DateTime CreatedAt, DateTime? NeededAt);
public sealed record MaterialPageDto(int Page, int PageSize, int Total, IReadOnlyList<MaterialSummaryDto> Items);
public sealed record MaterialAvailabilityLineDto(long RequestLineId, long ItemId, long UomId, decimal RequestedQty,
    decimal? ApprovedQty, decimal FulfilledQty, decimal? RemainingApprovedQty);
public sealed record MaterialAvailabilityDto(long RequestId, long WarehouseId, string Notice, object Stock, IReadOnlyList<MaterialAvailabilityLineDto> Lines);
public sealed class MaterialPage
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? BranchId { get; set; }
    public long? DepartmentId { get; set; }
    public long? RequesterId { get; set; }
    public long? ItemId { get; set; }
    public long? IncidentId { get; set; }
    public string? Status { get; set; }
    public string? Keyword { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
