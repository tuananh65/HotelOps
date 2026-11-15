namespace HotelOps.Api.DTOs.OperationalTasks;

public sealed record AssignOperationalTaskRequest(long AssigneeId, string? Reason);
public sealed record TaskExecutorDto(long UserId, string Name, int OpenTasks, long LoggedSecondsToday);
public sealed record TaskAssignmentReviewDto(long TaskId, long BranchId, string State, long? AssigneeId,
    DateTimeOffset AsOf, string Timezone, IReadOnlyList<TaskExecutorDto> Executors, int TotalCount, int Page, int PageSize);
public sealed record TaskAssignmentResult(long TaskId, long BranchId, long AssigneeId, string State, string MessageCode);
