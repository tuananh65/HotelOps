namespace HotelOps.Api.DTOs.Incidents;

public sealed record IncidentTriageOptionDto(long PriorityId, string Name, DateTime ResponseDeadlineAt,
    DateTime ResolutionDeadlineAt, string TargetVersion);
