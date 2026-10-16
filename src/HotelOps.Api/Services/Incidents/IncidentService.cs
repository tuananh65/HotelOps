using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed class IncidentService : IIncidentService
{
    private readonly HotelOpsDbContext _dbContext;

    public IncidentService(HotelOpsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<IncidentListDto>> GetIncidentsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Incidents
            .AsNoTracking()
            .OrderByDescending(incident => incident.ReportedAt)
            .ThenByDescending(incident => incident.IncidentId)
            .Select(incident => new IncidentListDto
            {
                IncidentId = incident.IncidentId,
                IncidentNo = incident.IncidentNo,
                BranchId = incident.BranchId,
                BranchName = incident.Branch.BranchName,
                RoomId = incident.RoomId,
                RoomNumber = incident.Room == null ? null : incident.Room.RoomNumber,
                AreaId = incident.AreaId,
                AreaName = incident.Area == null ? null : incident.Area.AreaName,
                AssetId = incident.AssetId,
                AssetName = incident.Asset == null ? null : incident.Asset.AssetName,
                IncidentTitle = incident.IncidentTitle,
                SeverityId = incident.SeverityId,
                SeverityName = incident.Severity == null ? null : incident.Severity.DisplayName,
                PriorityId = incident.PriorityId,
                PriorityName = incident.Priority == null ? null : incident.Priority.DisplayName,
                Status = incident.Status,
                ReportedBy = incident.ReportedBy,
                ReportedByName = incident.ReportedByNavigation.FullName,
                DiscoveredAt = incident.DiscoveredAt,
                ReportedAt = incident.ReportedAt,
                ResolvedAt = incident.ResolvedAt,
                ClosedAt = incident.ClosedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IncidentDetailDto?> GetIncidentAsync(long id, CancellationToken cancellationToken)
    {
        return await _dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.IncidentId == id)
            .Select(incident => new IncidentDetailDto
            {
                IncidentId = incident.IncidentId,
                IncidentNo = incident.IncidentNo,
                BranchId = incident.BranchId,
                BranchName = incident.Branch.BranchName,
                RoomId = incident.RoomId,
                RoomNumber = incident.Room == null ? null : incident.Room.RoomNumber,
                AreaId = incident.AreaId,
                AreaName = incident.Area == null ? null : incident.Area.AreaName,
                AssetId = incident.AssetId,
                AssetName = incident.Asset == null ? null : incident.Asset.AssetName,
                IncidentTitle = incident.IncidentTitle,
                SeverityId = incident.SeverityId,
                SeverityName = incident.Severity == null ? null : incident.Severity.DisplayName,
                PriorityId = incident.PriorityId,
                PriorityName = incident.Priority == null ? null : incident.Priority.DisplayName,
                Status = incident.Status,
                ReportedBy = incident.ReportedBy,
                ReportedByName = incident.ReportedByNavigation.FullName,
                DiscoveredAt = incident.DiscoveredAt,
                ReportedAt = incident.ReportedAt,
                ResolvedAt = incident.ResolvedAt,
                ClosedAt = incident.ClosedAt,
                Description = incident.Description,
                ClosureNote = incident.ClosureNote,
                Events = incident.IncidentEvents
                    .OrderBy(incidentEvent => incidentEvent.OccurredAt)
                    .ThenBy(incidentEvent => incidentEvent.EventId)
                    .Select(incidentEvent => new IncidentEventDto
                    {
                        EventId = incidentEvent.EventId,
                        IncidentId = incidentEvent.IncidentId,
                        EventType = incidentEvent.EventType,
                        FromStatus = incidentEvent.FromStatus,
                        ToStatus = incidentEvent.ToStatus,
                        Note = incidentEvent.Note,
                        ActorId = incidentEvent.ActorId,
                        ActorName = incidentEvent.Actor.FullName,
                        OccurredAt = incidentEvent.OccurredAt
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
