using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Incidents;

public sealed class TriageIncidentRequest
{
    // TODO: Replace this temporary integration field with trusted current-user context.
    // ActorUserId is not proof of caller identity.
    public long ActorUserId { get; init; }

    [Required]
    public long? SeverityId { get; init; }

    [Required]
    public long? PriorityId { get; init; }
}
