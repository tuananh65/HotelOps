namespace HotelOps.Api.DTOs.OperationalTasks;

public sealed record OperationalTaskQuery(int Page = 1, int PageSize = 25, string? Search = null, string? Status = null, long? DepartmentId = null);
public sealed record OperationalTaskRow(long Id, string Code, long BranchId, long DepartmentId, string Title,
    string Status, int ProgressPercent, DateTime? PlannedStartAt, DateTime? DueAt, DateTime? CompletedAt);
public sealed record OperationalTaskPage(IReadOnlyList<OperationalTaskRow> Items, int TotalCount, int Page, int PageSize);
public sealed record OperationalTaskProgressRow(long Id, int ProgressPercent, string? FromStatus, string? ToStatus,
    string? Description, string? BlockerReason, long ReportedBy, DateTime ReportedAt);
public sealed record OperationalTaskDetail(OperationalTaskRow Task, string? Description, long? CategoryId, long? PriorityId,
    long? RoomId, long? AreaId, string? CompletionNote, IReadOnlyList<OperationalTaskProgressRow> Progress, int ProgressTotalCount);
