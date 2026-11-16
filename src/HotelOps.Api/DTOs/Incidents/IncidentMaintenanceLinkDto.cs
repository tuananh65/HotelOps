namespace HotelOps.Api.DTOs.Incidents;

public sealed record IncidentMaintenanceLinkDto(long WorkId,string WorkNo,string Status,DateTime CreatedAt);
