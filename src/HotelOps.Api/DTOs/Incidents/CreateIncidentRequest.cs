using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Incidents;

public sealed class CreateIncidentRequest
{
    // TODO: Replace integration-supplied branch/reporter with trusted user context.
    // ReportedBy is not proof of caller identity.
    public long BranchId { get; init; }
    public long ReportedBy { get; init; }

    [Required, MaxLength(200)]
    public string IncidentTitle { get; init; } = string.Empty;

    [Required]
    public string Description { get; init; } = string.Empty;

    [Required]
    public DateTime? DiscoveredAt { get; init; }
    public long? RoomId { get; init; }
    public long? AreaId { get; init; }
    public long? AssetId { get; init; }
    public long? SeverityId { get; init; }
    public long? PriorityId { get; init; }
}
