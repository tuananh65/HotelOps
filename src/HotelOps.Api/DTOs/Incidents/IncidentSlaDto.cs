namespace HotelOps.Api.DTOs.Incidents;

public sealed record IncidentSlaDto(DateTime? ResponseDeadlineAt, DateTime? ResolutionDeadlineAt,
    string? UnavailableReason);
