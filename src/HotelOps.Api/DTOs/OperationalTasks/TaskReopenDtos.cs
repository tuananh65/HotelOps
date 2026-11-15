using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.OperationalTasks;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReopenOperationalTaskRequest(long AssigneeId, string Reason, string Instructions);
public sealed record TaskReopenEventDto(long PreviousSubmissionId, long AssignmentId, long AssigneeId,
    long ReopenedBy, DateTimeOffset ReopenedAt, string Reason, string Instructions);
public sealed record TaskReopenReviewDto(long TaskId, long BranchId, string State, bool CanReopen,
    long? PreviousAssigneeId, IReadOnlyList<TaskExecutorDto> Executors, int TotalCount, int Page, int PageSize,
    IReadOnlyList<TaskReopenEventDto> History);
