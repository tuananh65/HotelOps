using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Incidents;

public sealed class TriageIncidentRequest
{
    // Compatibility field; RequireEntry binds it to the current authenticated actor.
    public long ActorUserId { get; init; }

    [Required]
    public long? SeverityId { get; init; }

    [Required]
    public long? PriorityId { get; init; }

    [Required, RegularExpression("^[0-9A-F]{16}$")]
    public string? ExpectedSlaTargetVersion { get; init; }

    [Required]
    public string? InitialActionPlan { get; init; }

    public DateTime? ResponseDeadlineAt { get; init; }
    public DateTime? ResolutionDeadlineAt { get; init; }
    public string? DeadlineChangeReason { get; init; }
}
