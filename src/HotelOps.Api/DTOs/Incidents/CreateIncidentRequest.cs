using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Incidents;

public sealed class CreateIncidentRequest
{
    // Optional legacy attribution; authority always comes from the validated session.
    [Range(1, long.MaxValue)]
    public long BranchId { get; init; }
    public long? ReportedBy { get; init; }

    [Required, MaxLength(200)]
    public string IncidentTitle { get; init; } = string.Empty;

    [Required]
    public string Description { get; init; } = string.Empty;

    [Required]
    public DateTime? DiscoveredAt { get; init; }
    [Range(1, long.MaxValue)]
    public long? RoomId { get; init; }
    [Range(1, long.MaxValue)]
    public long? AreaId { get; init; }
    [Range(1, long.MaxValue)]
    public long? AssetId { get; init; }
    [Range(1, long.MaxValue)]
    public long? SeverityId { get; init; }
    [Range(1, long.MaxValue)]
    public long? PriorityId { get; init; }
}
