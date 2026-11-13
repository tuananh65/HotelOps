using HotelOps.Api.DTOs.Incidents;

namespace HotelOps.Api.Services.Incidents;

public interface IIncidentService
{
    Task<IncidentOptionsDto> GetOptionsAsync(long? branchId, long? roomId, CancellationToken cancellationToken);
    Task<IncidentDetailDto?> ChangeIncidentStatusAsync(long id, ChangeIncidentStatusRequest request, CancellationToken cancellationToken);
    Task<IncidentDetailDto?> TriageIncidentAsync(long id, TriageIncidentRequest request, CancellationToken cancellationToken);
    Task<IncidentDetailDto> CreateIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken);
    Task<IncidentPagedResponse> GetIncidentsAsync(IncidentQueryRequest request, CancellationToken cancellationToken);
    Task<IncidentDetailDto?> GetIncidentAsync(long id, CancellationToken cancellationToken);
}
