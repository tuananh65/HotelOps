using HotelOps.Api.DTOs.Housekeeping;
using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.OperationalTasks;

public sealed record CreateOperationalTaskRequest(Guid RequestKey, long DepartmentId, long CategoryId, string Title,
    string? Description, string RequestedResult, IReadOnlyList<ProgressDefinition>? Criteria, long PriorityId,
    DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt, int? SlaMinutes, long? RoomId, long? AreaId, long? InternalRequestId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TaskRequestLink? RequestLink = null);
public sealed record TaskRequestLink(string Version, [property: JsonRequired] bool Mandatory);
public sealed record TaskRequestContext(long RequestId, long BranchId, long DepartmentId, string Title, string Description,
    long? PriorityId, long? RoomId, long? AreaId, DateTimeOffset? DueAt, string Version);
public sealed record PlanOperationalTaskRequest(long PriorityId, DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt, int? SlaMinutes, string? Reason);
public sealed record CancelOperationalTaskRequest(string Reason);
public sealed record TaskPlanningLookup(long Id, string Name);
public sealed record TaskPlanningOptions(IReadOnlyList<TaskPlanningLookup> Departments, IReadOnlyList<TaskPlanningLookup> Categories,
    IReadOnlyList<TaskPlanningLookup> Priorities, IReadOnlyList<TaskPlanningLookup> DefaultExecutors, bool RecurringAvailable);
public sealed record TaskCreatedDto(long TaskId, long BranchId, string Code, string State, string MessageCode);
public sealed record TaskPlanHistoryDto(long AuditId, DateTimeOffset At, long ActorId, long? PreviousPriorityId,
    DateTimeOffset? PreviousPlannedStartAt, DateTimeOffset? PreviousDueAt, int? PreviousSlaMinutes,
    long? PriorityId, DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt, int? SlaMinutes, string? Reason);
public sealed record TaskPlanDto(long TaskId, long BranchId, string State, string? RequestedResult, long? PriorityId,
    DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt, int? SlaMinutes, bool CanManage, bool CanCancel,
    IReadOnlyList<TaskPlanHistoryDto> History, int HistoryTotalCount, int Page, int PageSize, long? SeriesId);
