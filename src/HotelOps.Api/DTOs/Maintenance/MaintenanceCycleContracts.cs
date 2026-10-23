namespace HotelOps.Api.DTOs.Maintenance;

public sealed class ReopenMaintenanceWorkRequest : MaintenanceActorRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed record MaintenanceCycleSnapshot(long? AssigneeId, long? DepartmentId, long? PriorityId,
    string? Title, string? Instruction, DateTime? PlannedStartAt, DateTime? PlannedEndAt,
    DateTime? ActualStartAt, DateTime? ResolvedAt, string? ResolutionNote, long? ReviewedBy, DateTime? ReviewedAt,
    DateTime? IncidentResolvedAt, DateTime? IncidentClosedAt, string? IncidentClosureNote);
public sealed record MaintenanceCycleDto(long CycleId, long MaintenanceWorkId, int CycleNo, bool IsLegacySnapshot,
    DateTime RecordedAt, long RecordedBy, DateTime? OpenedAt, long? OpenedBy, string? ReopenReason,
    string? StartingWorkStatus, string? StartingIncidentStatus, DateTime? FinalizedAt,
    string? TerminalStatus, DateTime? EndedAt, long? EndedBy, string? TerminalReason,
    MaintenanceCycleSnapshot Snapshot, int LogCount, int MaterialUsageCount);
public sealed record MaintenanceCyclePage(int Page, int PageSize, int TotalCount, IReadOnlyList<MaintenanceCycleDto> Items);
