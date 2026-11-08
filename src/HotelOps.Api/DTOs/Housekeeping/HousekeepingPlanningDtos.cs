namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record HousekeepingPlanningQuery(string? Search = null, DateTime? From = null, DateTime? To = null, int Page = 1, int PageSize = 20);
public sealed record PlanningTaskDto(long Id, string Number, long RoomId, string RoomNumber, long BranchId, string RecordedStatus, DateTime? DueAt, string? WorkflowState = null);
public sealed record PlanningShiftDto(long Id, long UserId, DateTime From, DateTime To, string RecordedShiftStatus, string RecordedAssignmentStatus);
public sealed record PlanningStaffDto(long Id, string Name, long? DepartmentId, int OpenTasks, int OpenTasksInWindow);
public sealed record PlanningChecklistDto(long Id, string Name, long VersionId, int VersionNumber, int ActiveCriteria);
public sealed record HousekeepingPlanningDto(PlanningTaskDto Task, IReadOnlyList<PlanningStaffDto> Candidates,
    IReadOnlyList<PlanningShiftDto> RecordedShifts, int Total, HousekeepingPlanningQuery Filters,
    PlanningChecklistDto? Checklist, DateTime CapturedAt, bool WorkflowStateVerified = false, string AssignmentCapability = "UNAVAILABLE");
