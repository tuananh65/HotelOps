namespace HotelOps.Api.Compact.Contracts;

public sealed record CreateRefillWorkflowRequest(long AllocationId,IReadOnlyList<SupplyIssueQuantity> Items,string Reason,Guid OperationKey);
public sealed record CreateReplenishmentWorkflowRequest(long BranchId,long WarehouseId,IReadOnlyList<SupplyIssueQuantity> Items,string Reason,Guid OperationKey,long? PriorityId=null);
public sealed record RequestWorkflowAction(string RowVersion,Guid OperationKey,string? Reason=null,IReadOnlyList<SupplyIssueQuantity>? ApprovedItems=null);
public sealed record ReceiveReplenishmentWorkflowRequest(string RowVersion,Guid OperationKey,IReadOnlyList<SupplyIssueQuantity> Items,string? SupplierName=null,string? Note=null);
public sealed record RequestWorkflowSummary(long WorkId,string Number,string RequestType,string Title,long BranchId,long? RoomId,long CreatedBy,long? RequesterDepartmentId,long? TargetDepartmentId,string Status,string? QueueRole,long? HandlerUserId,long? PriorityId,DateTime CreatedAt,DateTime UpdatedAt,Guid? CorrelationId,string RowVersion,bool Structured,string? CreatedByName=null,string? HandlerName=null);
public sealed record RequestWorkflowLineDto(long ItemId,string Name,long UnitId,decimal RequestedQuantity,decimal? ApprovedQuantity,decimal StockAtRequest,decimal MinimumAtRequest,decimal? CurrentStock,decimal ReceivedQuantity);
public sealed record RequestWorkflowHistoryDto(long EventId,string Action,string? PreviousStatus,string NewStatus,string? PreviousQueue,string? NewQueue,long? PreviousHandler,long? NewHandler,long ActorUserId,string ActorRole,DateTime At,string? Reason,long? DocumentId,Guid CorrelationId,string? PreviousVersion);
public sealed record RequestWorkflowDetail(RequestWorkflowSummary Request,string? Description,long? AllocationId,long? WarehouseId,long? DocumentId,long? ParentRequestWorkId,IReadOnlyList<RequestWorkflowLineDto> Items,IReadOnlyList<RequestWorkflowHistoryDto> History,IReadOnlyList<string> Actions);
