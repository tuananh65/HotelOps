namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record InspectionReassignmentRequest(long InspectorId, string CycleETag, string Reason);
public sealed record InspectionReassignmentOptions(long Id, long BranchId, long TaskId, long RoomId, long VersionId, long InspectorId,
    string State, DateTime? DueAt, string SourceETag, string CycleETag, IReadOnlyList<ReinspectionAssigneeDto> Inspectors, bool CanReassign);
public sealed record InspectionReassignmentResult(long Id, long TaskId, long BranchId, long RoomId, long VersionId,
    long PreviousInspectorId, long InspectorId, string State, string SourceETag, Guid OperationId,
    string Notification = "PENDING_DELIVERY", string Contract = "FE05_INSPECTION_REASSIGNMENT_V1");
