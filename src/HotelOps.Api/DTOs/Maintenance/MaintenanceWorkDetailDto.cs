namespace HotelOps.Api.DTOs.Maintenance;

public sealed class MaintenanceWorkDetailDto
{
    public string? ExecutionETag { get; set; }
    public bool CanResume { get; set; }
    public bool CanStart { get; set; }
    public bool CanHold { get; set; }
    public bool MaterialUsageAvailable { get; set; }
    public long MaintenanceWorkId { get; init; }
    public string? AssigneeName { get; init; }
    public string? PriorityName { get; init; }
    public string? RoomNumber { get; init; }
    public string? AreaName { get; init; }
    public string? AssetCode { get; init; }
    public string? AssetName { get; init; }
    public string? Source { get; init; }
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
    public long CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Instruction { get; init; }
    public string? ResolutionNote { get; init; }
    public IReadOnlyList<MaintenanceWorkLogDto> MaintenanceWorkLogs { get; init; } = [];
}
