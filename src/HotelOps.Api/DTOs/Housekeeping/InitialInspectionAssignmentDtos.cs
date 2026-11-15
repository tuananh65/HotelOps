namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record InitialInspectionAssignmentRequest(long InspectorId, long ChecklistVersionId, string CycleETag, DateTime DueAt, string Reason);
public sealed record InitialInspectionAssignmentResult(long Id, long TaskId, long BranchId, long RoomId, long VersionId, long InspectorId,
    string State, string SourceETag, Guid OperationId, string Notification = "PENDING_DELIVERY", string Contract = "FE05_INITIAL_INSPECTION_ASSIGNMENT_V1");
public sealed record InitialInspectionQueueRow(long TaskId, long RoomId, string RoomNumber, DateTime CreatedAt);
public sealed record InitialInspectionQueue(IReadOnlyList<InitialInspectionQueueRow> Items, int TotalCount, int Page, int PageSize);
public sealed record InitialInspectionOptions(long TaskId, long BranchId, long RoomId, string RoomNumber, string TaskETag, string CycleETag,
    long? ChecklistVersionId, string? ChecklistName, int? VersionNo, long? ExistingInspectionId, long? ExistingInspectorId,
    IReadOnlyList<ReinspectionAssigneeDto> Inspectors, bool CanAssign, string? UnavailableReason);
