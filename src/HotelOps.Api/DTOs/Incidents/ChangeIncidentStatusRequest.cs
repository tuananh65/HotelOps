using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Incidents;

public sealed class ChangeIncidentStatusRequest
{
    // TODO: Replace this temporary integration field with trusted current-user context.
    // ActorUserId does not prove caller identity.
    public long ActorUserId { get; init; }

    [Required, MaxLength(40)]
    public string Status { get; init; } = string.Empty;

    public string? Note { get; init; }
}
