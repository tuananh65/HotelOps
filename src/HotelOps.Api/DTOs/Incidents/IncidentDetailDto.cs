namespace HotelOps.Api.DTOs.Incidents;

public sealed class IncidentDetailDto
{
    public IReadOnlyList<IncidentMaintenanceLinkDto> MaintenanceWorks { get; set; } = [];
    public string? SubmissionWorkflow { get; set; }
    public string? AssignmentWorkflow { get; set; }
    public bool CanAssign { get; set; }
    public bool CanCancelTriage { get; set; }
    public bool CanManageRoomBlocks { get; set; }
    public IncidentSlaDto? Sla { get; set; }
    public string? TriageETag { get; set; }
    public bool CanTriage { get; set; }
    public string? TriageUnavailableReason { get; set; }
    public IReadOnlyList<IncidentTriageOptionDto> TriageOptions { get; set; } = [];
    public long IncidentId { get; init; }
    public string IncidentNo { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public long? RoomId { get; init; }
    public string? RoomNumber { get; init; }
    public long? AreaId { get; init; }
    public string? AreaName { get; init; }
    public long? AssetId { get; init; }
    public string? AssetName { get; init; }
    public string IncidentTitle { get; init; } = string.Empty;
    public long? SeverityId { get; init; }
    public string? SeverityName { get; init; }
    public long? PriorityId { get; init; }
    public string? PriorityName { get; init; }
    public long? CategoryId { get; set; }
    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryUnavailableReason { get; set; }
    public string Status { get; init; } = string.Empty;
    public long ReportedBy { get; init; }
    public string ReportedByName { get; init; } = string.Empty;
    public DateTime DiscoveredAt { get; init; }
    public DateTime ReportedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ClosureNote { get; init; }
    public IReadOnlyList<IncidentEventDto> Events { get; init; } = [];
}
