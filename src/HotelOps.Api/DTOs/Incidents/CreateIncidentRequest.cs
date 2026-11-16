using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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
    [Range(1, long.MaxValue)]
    public long? CategoryId { get; init; }
    public long? InternalRequestId { get; init; }
    public IncidentRequestLink? RequestLink { get; init; }
    public IncidentOccurrenceLink? OccurrenceLink { get; init; }
    [MaxLength(4096)]
    public string? SimilarReviewToken { get; init; }
    public IReadOnlyList<HotelOps.Api.DTOs.Housekeeping.EvidenceUpload>? Files { get; init; }
}

public sealed record IncidentRequestLink(string Version, [property: JsonRequired] bool Mandatory);
public sealed record IncidentOccurrenceLink(long RunId,string Version,string FailedCheckKey);
