using HotelOps.Api.DTOs.Incidents;

namespace HotelOps.Api.Services.Incidents;

public interface IIncidentService
{
    Task<IReadOnlyList<IncidentListDto>> GetIncidentsAsync(CancellationToken cancellationToken);
    Task<IncidentDetailDto?> GetIncidentAsync(long id, CancellationToken cancellationToken);
}
