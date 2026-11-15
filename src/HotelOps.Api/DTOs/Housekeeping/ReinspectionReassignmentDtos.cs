namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record ReinspectionReassignmentRequest(long InspectorId,string CycleETag,string ReworkETag,string BindingETag,string Reason);
public sealed record ReinspectionReassignmentOptions(long Id,long BranchId,long TaskId,long RoomId,long VersionId,long InspectorId,
    string State,DateTime? DueAt,string SourceETag,string CycleETag,IReadOnlyList<ReinspectionAssigneeDto> Inspectors,bool CanReassign,
    long ReworkId,string ReworkETag,string BindingETag);
public sealed record ReinspectionReassignmentResult(long Id,long TaskId,long BranchId,long RoomId,long VersionId,
    long PreviousInspectorId,long InspectorId,string State,string SourceETag,Guid OperationId,long ReworkId,string ReworkETag,string BindingETag,
    string Notification="PENDING_DELIVERY",string Contract="FE05_REINSPECTION_REASSIGNMENT_V1");
