namespace HotelOps.Api.DTOs.OperationalTasks;

public sealed record TaskRecurrenceRequest(string Rule, IReadOnlyList<int>? Weekdays, int? MonthDay, long DefaultAssigneeId, DateOnly? EndDate);
public sealed record CreateTaskSeriesRequest(CreateOperationalTaskRequest Template, TaskRecurrenceRequest Recurrence);
public sealed record StopTaskSeriesRequest(string? Reason, long? TaskId = null);
public sealed record TaskSeriesDto(long SeriesId, long BranchId, string Title, string Rule, IReadOnlyList<int> Weekdays,
    int? MonthDay, long DefaultAssigneeId, string DefaultAssigneeName, DateTimeOffset FirstDueAt, DateTimeOffset? NextDueAt,
    DateOnly? EndDate, bool IsActive, int OccurrenceCount, string ETag);
public sealed record TaskSeriesPage(IReadOnlyList<TaskSeriesDto> Items, int TotalCount, int Page, int PageSize);
