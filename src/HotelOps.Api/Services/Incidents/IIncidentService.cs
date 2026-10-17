using HotelOps.Api.DTOs.Incidents;

namespace HotelOps.Api.Services.Incidents;

public interface IIncidentService
{
    Task<IncidentDetailDto?> TriageIncidentAsync(long id, TriageIncidentRequest request, CancellationToken cancellationToken);
    Task<IncidentDetailDto> CreateIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken);
    Task<IncidentPagedResponse> GetIncidentsAsync(IncidentQueryRequest request, CancellationToken cancellationToken);
    Task<IncidentDetailDto?> GetIncidentAsync(long id, CancellationToken cancellationToken);
}
