namespace HotelOps.Api.DTOs.Maintenance;

public sealed class MaintenanceWorkListDto
{
    public long? OccurrenceId { get; init; }
    public long? BranchId { get; init; }
    public HotelOps.Api.Services.Maintenance.MaintenanceDeadlineView? Deadline { get; set; }
    public long? MaintenanceWorkId { get; init; }
    public string? Source { get; init; }
    public string? AssigneeName { get; init; }
    public string? PriorityName { get; init; }
    public string? RoomNumber { get; init; }
    public string? AreaName { get; init; }
    public string? AssetCode { get; init; }
    public string? AssetName { get; init; }
    public string WorkNo { get; init; } = string.Empty;
    public long? IncidentId { get; init; }
    public long? AssigneeId { get; init; }
    public long? DepartmentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public long? PriorityId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? PlannedStartAt { get; init; }
    public DateTime? PlannedEndAt { get; init; }
    public DateTime? ActualStartAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public long? ReviewedBy { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public long? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
}
