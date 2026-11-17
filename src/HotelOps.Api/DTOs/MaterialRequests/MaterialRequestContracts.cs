using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.MaterialRequests;

public sealed record MaterialLineWrite(long? RequestLineId, int LineNo, long ItemId, long UomId, decimal RequestedQty, string? Note);
public sealed record MaterialRequestWrite(long ActorUserId, long BranchId, long DepartmentId, long? SourceWarehouseId,
    string Purpose, DateTime? NeededAt, long? PriorityId, long? ReceivingRoomId, long? ReceivingAreaId, long? IncidentId,
    IReadOnlyList<MaterialLineWrite> Lines, bool Submit = false, long? AssignedApproverUserId = null, string? ExpectedRowVersion = null,
    long? InternalRequestId = null, MaterialRequestSourceLink? RequestLink = null);
public sealed record MaterialRequestSourceLink(string Version, [property: JsonRequired] bool Mandatory);
public sealed record MaterialCommand(long ActorUserId, string ExpectedRowVersion, string? Reason = null, long? AssignedApproverUserId = null);
public sealed record MaterialApprovalLine(long RequestLineId, decimal ApprovedQty);
public sealed record MaterialApproval(long ActorUserId, string ExpectedRowVersion, IReadOnlyList<MaterialApprovalLine> Lines, string? Reason = null);
public sealed record MaterialIssueLine(long RequestLineId, decimal Quantity);
public sealed record MaterialIssue(long ActorUserId, string ExpectedRowVersion, string Mode, DateOnly DocumentDate, IReadOnlyList<MaterialIssueLine>? Lines = null, string? ReceiverName = null, long? WarehouseId = null);
public sealed record MaterialLineDto(long RequestLineId, int LineNo, long ItemId, long UomId, decimal RequestedQty,
    decimal? ApprovedQty, decimal FulfilledQty, decimal? HistoricalRemainingQty, decimal OpenFulfillableQty, bool IsActive, string? Note,
    string? ItemName = null, string? UnitName = null);
public sealed record MaterialCycleLineDto(long RequestLineId, int LineNo, long ItemId, long UomId, decimal RequestedQty, decimal? ApprovedQty, string? Note);
public sealed record MaterialDecisionDto(long ApprovalId, long? ApprovalCycleId, long ApproverId, string Decision, string? Note, DateTime DecidedAt);
public sealed record MaterialCycleDto(long ApprovalCycleId, int RevisionNo, long SubmittedBy, long AssignedApproverUserId,
    string Status, DateTime SubmittedAt, DateTime? ResolvedAt, IReadOnlyList<MaterialCycleLineDto> Lines);
public sealed record MaterialFulfillmentLineDto(long FulfillmentLineId, long RequestLineId, long ItemId, long UomId, decimal IssuedQty, decimal? ReceivedQty, long? StockDocumentLineId);
public sealed record MaterialFulfillmentDto(long FulfillmentId, string FulfillmentNo, long RequestId, long WarehouseId,
    long FulfilledBy, long? ReceivedBy, string Status, DateTime? FulfilledAt, DateTime? ReceivedAt, long? StockDocumentId,
    string? StockDocumentNo, IReadOnlyList<MaterialFulfillmentLineDto> Lines, string? ReceiverName = null);
public sealed record MaterialReturnDto(long DocumentId, string DocumentNo, string Status, long OriginalIssueLineId,
    long? ReturnLineId = null, long? ItemId = null, string? ItemName = null, string? UnitName = null,
    decimal? ActualQty = null, decimal? DamagedQty = null, DateTime? CreatedAt = null,
    DateTime? PostedAt = null, long? PostedBy = null);
public sealed record MaterialRequestDto(long RequestId, string RequestNo, string RawStatus, bool IsNewWorkflow, bool MutationEligible,
    string? MutationRestriction, string RowVersion, long BranchId, long DepartmentId, long RequesterId, long? SourceWarehouseId,
    string Purpose, DateTime? NeededAt, long? PriorityId, long? ReceivingRoomId, long? ReceivingAreaId, long? IncidentId,
    DateTime CreatedAt, DateTime? SubmittedAt, DateTime? CompletedAt, DateTime? UpdatedAt, string? CancellationReason,
    DateTime? RemainderClosedAt, long? RemainderClosedBy, string? RemainderCloseReason,
    IReadOnlyList<MaterialLineDto> Lines, IReadOnlyList<MaterialCycleDto> Cycles, IReadOnlyList<MaterialDecisionDto> Decisions,
    IReadOnlyList<MaterialFulfillmentDto> Fulfillments, IReadOnlyList<MaterialReturnDto> Returns)
{
    public bool LinkedWorkAvailable { get; init; }
    public MaterialLinkedIncidentDto? LinkedIncident { get; init; }
    public IReadOnlyList<MaterialLinkedRequestDto> LinkedInternalRequests { get; init; } = [];
}
public sealed record MaterialLinkedRequestDto(long RequestId, string RequestNo, string Status, bool Mandatory);
public sealed record MaterialLinkedIncidentDto(long IncidentId, string IncidentNo, string Title, string Status);
public sealed record MaterialAuditDto(long AuditId, string EntityType, string EntityId, string Action, long? ActorUserId,
    DateTime OccurredAt, string? OldValues, string? NewValues, string? ChangedFields);
public sealed record MaterialHistoryDto(MaterialRequestDto Request, IReadOnlyList<MaterialAuditDto> Audit);
public sealed record MaterialSummaryDto(long RequestId, string RequestNo, string RawStatus, long BranchId, long DepartmentId, long RequesterId, DateTime CreatedAt, DateTime? NeededAt,
    long? AssignedApproverUserId = null, IReadOnlyList<MaterialSummaryLineDto>? Quantities = null);
public sealed record MaterialSummaryLineDto(long RequestLineId, string ItemName, string UnitName,
    decimal RequestedQty, decimal? ApprovedQty, decimal IssuedQty);
public sealed record MaterialPageDto(int Page, int PageSize, int Total, IReadOnlyList<MaterialSummaryDto> Items);
public sealed record MaterialAvailabilityLineDto(long RequestLineId, long ItemId, long UomId, decimal RequestedQty,
    decimal? ApprovedQty, decimal FulfilledQty, decimal? RemainingApprovedQty);
public sealed record MaterialAvailabilityDto(long RequestId, long WarehouseId, string Notice, object Stock,
    IReadOnlyList<MaterialAvailabilityLineDto> Lines, IReadOnlyList<MaterialWarehouseAvailabilityDto>? Warehouses = null,
    string? RowVersion = null);
public sealed record MaterialWarehouseAvailabilityDto(long WarehouseId, string WarehouseName,
    IReadOnlyList<MaterialStockPreviewDto> Lines);
public sealed record MaterialStockPreviewDto(long RequestLineId, string ItemName, string UnitName, string BaseUnitName,
    decimal? RemainingApprovedQty, decimal AvailableBaseQty, decimal? InTransitBaseQty,
    decimal? FulfillableQty, string? UnavailableReason, string? TransitUnavailableReason, bool SharedItemStock);
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
