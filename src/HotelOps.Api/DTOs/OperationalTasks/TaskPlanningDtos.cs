using HotelOps.Api.DTOs.Housekeeping;

namespace HotelOps.Api.DTOs.OperationalTasks;

public sealed record CreateOperationalTaskRequest(Guid RequestKey, long DepartmentId, long CategoryId, string Title,
    string? Description, string RequestedResult, IReadOnlyList<ProgressDefinition>? Criteria, long PriorityId,
    DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt, int? SlaMinutes, long? RoomId, long? AreaId, long? InternalRequestId);
public sealed record PlanOperationalTaskRequest(long PriorityId, DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt, int? SlaMinutes, string? Reason);
public sealed record CancelOperationalTaskRequest(string Reason);
public sealed record TaskPlanningLookup(long Id, string Name);
public sealed record TaskPlanningOptions(IReadOnlyList<TaskPlanningLookup> Departments, IReadOnlyList<TaskPlanningLookup> Categories,
    IReadOnlyList<TaskPlanningLookup> Priorities);
public sealed record TaskCreatedDto(long TaskId, long BranchId, string Code, string State, string MessageCode);
public sealed record TaskPlanHistoryDto(long AuditId, DateTimeOffset At, long ActorId, long? PreviousPriorityId,
    DateTimeOffset? PreviousPlannedStartAt, DateTimeOffset? PreviousDueAt, int? PreviousSlaMinutes,
    long? PriorityId, DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt, int? SlaMinutes, string? Reason);
public sealed record TaskPlanDto(long TaskId, long BranchId, string State, string? RequestedResult, long? PriorityId,
    DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt, int? SlaMinutes, bool CanManage, bool CanCancel,
    IReadOnlyList<TaskPlanHistoryDto> History, int HistoryTotalCount, int Page, int PageSize);
