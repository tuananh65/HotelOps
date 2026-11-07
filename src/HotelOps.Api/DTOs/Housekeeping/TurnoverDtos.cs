namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record TurnoverQuery(long? BranchId = null, DateTime? From = null, DateTime? To = null,
    string? Result = null, int Page = 1, int PageSize = 20, int TimelinePage = 1, int TimelinePageSize = 50);
public sealed record TurnoverCoverage(string Cleaning, string Inspection, string Rework, string Readiness, string RoomStatusHistory);
public sealed record TurnoverCycle(long Id, long RoomId, long BranchId, string RoomNumber, string Number,
    string CleaningType, string RecordedStatus, string? WorkflowState, DateTime CreatedAtUtc, DateTime? StartUtc,
    DateTime? EndUtc, bool? IsCurrentCycle, string? Result, int? ReworkCount, string? CancellationReason,
    IReadOnlyList<TurnoverAssignee> Assignees);
public sealed record TurnoverAssignee(long Id, long UserId, string Name, string Status, DateTime AssignedAtUtc, DateTime? EndedAtUtc);
public sealed record TurnoverPage(long RoomId, long BranchId, IReadOnlyList<TurnoverCycle> Items, int Total,
    TurnoverQuery Filters, TurnoverCoverage Coverage);
public sealed record TurnoverInspection(long Id, long CleaningTaskId, long RoomId, long VersionId, int VersionNumber,
    long InspectorId, string RecordedStatus, DateTime AtUtc, string? Result = null, string Coverage = "NOT_ACCEPTED");
public sealed record TurnoverRework(long Id, long InspectionId, long CleaningTaskId, long? AssigneeId,
    string RecordedStatus, string? WorkflowState, DateTime RequestedAtUtc, DateTime? CompletedAtUtc, string? Note);
public sealed record TurnoverEvent(string SourceType, long SourceId, string Event, DateTime AtUtc,
    long? ActorId, string? State, string? Note);
public sealed record TurnoverDetail(TurnoverCycle Cycle, IReadOnlyList<TurnoverEvent> Timeline, int TimelineTotal,
    int TimelinePage, int TimelinePageSize, IReadOnlyList<TurnoverInspection> Inspections,
    IReadOnlyList<TurnoverRework> Reworks, IReadOnlyList<HotelOps.Api.DTOs.HotelStructure.HistoryEvidenceDto> Evidence,
    TurnoverCoverage Coverage);
