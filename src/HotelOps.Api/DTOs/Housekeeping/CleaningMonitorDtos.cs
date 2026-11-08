namespace HotelOps.Api.DTOs.Housekeeping;

public sealed record CleaningMonitorQuery(DateOnly? FromDate = null, DateOnly? ToDate = null,
    long? FloorId = null, string? Status = null, long? AssigneeId = null, long? PriorityId = null,
    bool? Overdue = null, string? Search = null, string? Kind = null, int Page = 1, int PageSize = 20);

public sealed record CleaningMonitorItem(string Kind, long Id, long CleaningTaskId, string TaskNumber,
    long RoomId, string RoomNumber, long FloorId, string? FloorName, string RecordedStatus,
    string? WorkflowState, long? PriorityId, string? PriorityCode, long? AssigneeId, string? AssigneeName,
    DateTime? DueAtUtc, bool? IsOverdue, int? ProgressPercent, string? BlockerCode, string? BlockerNote,
    string? Description, bool? IsCurrentCycle = null, bool? HasUnresolvedBlocker = null,
    string? EffectiveReadiness = null, string ReadinessCoverage = "UNAVAILABLE");

public sealed record CleaningMonitorPage(IReadOnlyList<CleaningMonitorItem> Items, int Total,
    int Page, int PageSize, DateOnly FromDate, DateOnly ToDate, string Timezone, DateTime AsOfUtc);

public sealed record MonitorAssignment(long Id, long AssigneeId, string Status, DateTime AssignedAtUtc,
    DateTime? EndedAtUtc, string? Reason);
public sealed record MonitorProgress(long Id, int Percent, string? Status, string? BlockerCode,
    string? Note, long LoggedBy, DateTime LoggedAtUtc);
public sealed record MonitorAudit(long Id, string EntityType, string Action, long? ActorId, DateTime OccurredAtUtc);
public sealed record CleaningMonitorTimelineQuery(int TimelinePage = 1, int TimelinePageSize = 100);
public sealed record CleaningMonitorDetail(CleaningMonitorItem Item, IReadOnlyList<MonitorAssignment> Assignments,
    int AssignmentCount, IReadOnlyList<MonitorProgress> Progress, int ProgressCount,
    IReadOnlyList<MonitorAudit> Audit, int AuditCount, int TimelineLimit, DateTime AsOfUtc, int TimelinePage = 1);
