namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record InspectionReadQuery(string? Search = null, int Page = 1, int PageSize = 20);
public sealed record InspectionReadPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record InspectionSeverityDto(long Id, string Code, string Name);
public sealed record AssignedInspectionDto(long Id, string Number, long BranchId, long RoomId, string RoomNumber,
    long CleaningTaskId, long InspectorId, string InspectorName, long VersionId, string State, DateTime? DueAt);
public sealed record InspectionAssignmentTaskDto(long Id, string Number, long BranchId, long RoomId, string RoomNumber,
    string? State, bool PendingReinspection);
