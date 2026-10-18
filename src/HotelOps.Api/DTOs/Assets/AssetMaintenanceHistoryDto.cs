namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetMaintenanceHistoryDto
{
    public string Type { get; init; } = string.Empty;
    public long SourceId { get; init; }
    // Work.CreatedAt, Run.PlannedAt or ConditionHistory.ChangedAt respectively.
    public DateTime OccurredAt { get; init; }
    public long? IncidentId { get; init; }
    public long? MaintenanceWorkId { get; init; }
    public long? ScheduleId { get; init; }
    public string? WorkNo { get; init; }
    public string? Status { get; init; }
    public long? ActorUserId { get; init; }
    public string? Note { get; init; }
    public string? Result { get; init; }
    public string? OldCondition { get; init; }
    public string? NewCondition { get; init; }
}
