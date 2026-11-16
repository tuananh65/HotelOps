namespace HotelOps.Api.DTOs.OperationalTasks;

public sealed record TaskWorkloadQuery(DateOnly? From = null, DateOnly? To = null,
    long? DepartmentId = null, long? EmployeeId = null, string? Status = null,
    long? PriorityId = null, DateOnly? DeadlineFrom = null, DateOnly? DeadlineTo = null,
    bool OverdueOnly = false, string? Search = null, int Page = 1, int PageSize = 20,
    string Calendar = "Asia/Ho_Chi_Minh");

public sealed record TaskWorkloadEmployee(long UserId, string Name, string EmployeeCode,
    int OpenTasks, int OverdueTasks, int ClosedTasks, long ClosedEffortSeconds, long ProvisionalSeconds);

public sealed record TaskWorkloadPage(long BranchId, DateOnly From, DateOnly To, DateTimeOffset AsOf,
    IReadOnlyList<TaskWorkloadEmployee> Items, int TotalCount, int Page, int PageSize, string? MessageCode,
    string Calendar = "Asia/Ho_Chi_Minh");

public sealed record TaskWorkloadTask
{
    public long TaskId { get; init; }
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public long DepartmentId { get; init; }
    public string Department { get; init; } = "";
    public string State { get; init; } = "";
    public long? PriorityId { get; init; }
    public string? Priority { get; init; }
    public DateTime? Deadline { get; init; }
    public long? CurrentAssigneeId { get; init; }
    public long? CompletedBy { get; init; }
    public DateTime? CompletedAt { get; init; }
    public bool Overdue { get; init; }
}

public sealed record TaskWorkloadInterval(long IntervalId, long TaskId, DateTimeOffset Start,
    DateTimeOffset? End, long ClosedSeconds, long ProvisionalSeconds);

public sealed record TaskWorkloadShift(long WorkdayId, DateOnly WorkDate, long DepartmentId,
    DateTimeOffset Start, DateTimeOffset? End, long ClosedSeconds, long ProvisionalSeconds);

public sealed record TaskWorkloadDetail(long BranchId, long UserId, string Name, DateOnly From,
    DateOnly To, DateTimeOffset AsOf, IReadOnlyList<TaskWorkloadTask> Tasks, int TaskCount,
    IReadOnlyList<TaskWorkloadInterval> Intervals, int IntervalCount,
    IReadOnlyList<TaskWorkloadShift>? Shifts, int? ShiftCount, int Page, int PageSize,
    string Calendar = "Asia/Ho_Chi_Minh");
