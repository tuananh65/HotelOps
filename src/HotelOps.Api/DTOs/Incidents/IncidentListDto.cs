namespace HotelOps.Api.DTOs.Incidents;

public sealed class IncidentListDto
{
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
    public string Status { get; init; } = string.Empty;
    public long ReportedBy { get; init; }
    public string ReportedByName { get; init; } = string.Empty;
    public DateTime DiscoveredAt { get; init; }
    public DateTime ReportedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
}
