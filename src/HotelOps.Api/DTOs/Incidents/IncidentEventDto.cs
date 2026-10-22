namespace HotelOps.Api.DTOs.Incidents;

public sealed class IncidentEventDto
{
    public long EventId { get; init; }
    public long IncidentId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string? FromStatus { get; init; }
    public string? ToStatus { get; init; }
    public string? Note { get; init; }
    public long ActorId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
}
