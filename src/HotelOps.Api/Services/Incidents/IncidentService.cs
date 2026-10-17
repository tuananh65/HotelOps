using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Entities;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;

namespace HotelOps.Api.Services.Incidents;

public sealed class IncidentService : IIncidentService
{
    private readonly HotelOpsDbContext _dbContext;

    public IncidentService(HotelOpsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IncidentDetailDto> CreateIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        await ValidateCreateAsync(request, cancellationToken);

        var reportedAt = DateTime.UtcNow;
        var prefix = "INC-" + reportedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize allocation across application instances until this transaction ends.
        // Range locks also protect allocation against writers that do not take the application lock.
        var resource = "HotelOps:IncidentNo:" + prefix;
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = {resource},
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 50001, 'Could not acquire incident number allocation lock.', 1;",
            cancellationToken);

        var maximum = await _dbContext.Database.SqlQuery<long>($@"
            SELECT COALESCE(MAX(TRY_CONVERT(bigint, SUBSTRING(incident_no, 14, 40))), CAST(0 AS bigint)) AS [Value]
            FROM incidents WITH (UPDLOCK, HOLDLOCK)
            WHERE incident_no LIKE {prefix + "%"}")
            .SingleAsync(cancellationToken);
        var number = prefix + checked(maximum + 1).ToString("D3", CultureInfo.InvariantCulture);

        var incident = new Incident
        {
            IncidentNo = number,
            BranchId = request.BranchId,
            ReportedBy = request.ReportedBy,
            IncidentTitle = request.IncidentTitle,
            Description = request.Description,
            DiscoveredAt = request.DiscoveredAt!.Value,
            RoomId = request.RoomId,
            AreaId = request.AreaId,
            AssetId = request.AssetId,
            SeverityId = request.SeverityId,
            PriorityId = request.PriorityId,
            Status = "OPEN",
            ReportedAt = reportedAt,
            ResolvedAt = null,
            ClosedAt = null,
            ClosureNote = null
        };
        incident.IncidentEvents.Add(new IncidentEvent
        {
            EventType = "CREATED",
            FromStatus = null,
            ToStatus = "OPEN",
            ActorId = request.ReportedBy,
            OccurredAt = reportedAt,
            Note = null
        });
        _dbContext.Incidents.Add(incident);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.AuditLogs.Add(new AuditLog
        {
            BranchId = incident.BranchId,
            ActorUserId = request.ReportedBy,
            EntityType = "incidents",
            EntityId = incident.IncidentId.ToString(CultureInfo.InvariantCulture),
            Action = "CREATE",
            OldValues = null,
            ChangedFields = null,
            OccurredAt = reportedAt,
            NewValues = JsonSerializer.Serialize(new
            {
                incident_no = incident.IncidentNo,
                status = incident.Status,
                room_id = incident.RoomId
            })
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await GetIncidentAsync(incident.IncidentId, cancellationToken)
            ?? throw new InvalidOperationException("Created incident could not be read.");
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task ValidateCreateAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Branches.AsNoTracking().AnyAsync(x => x.BranchId == request.BranchId, cancellationToken))
            throw new ValidationException("BranchId does not exist.");
        if (!await _dbContext.Users.AsNoTracking().AnyAsync(x => x.UserId == request.ReportedBy, cancellationToken))
            throw new ValidationException("ReportedBy does not exist.");
        if (request.RoomId is long roomId && !await _dbContext.Rooms.AsNoTracking()
            .AnyAsync(x => x.RoomId == roomId && x.BranchId == request.BranchId, cancellationToken))
            throw new ValidationException("RoomId must identify a room in the specified branch.");
        if (request.AreaId is long areaId && !await _dbContext.Areas.AsNoTracking()
            .AnyAsync(x => x.AreaId == areaId && x.BranchId == request.BranchId, cancellationToken))
            throw new ValidationException("AreaId must identify an area in the specified branch.");
        if (request.AssetId is long assetId && !await _dbContext.Assets.AsNoTracking()
            .AnyAsync(x => x.AssetId == assetId && x.BranchId == request.BranchId, cancellationToken))
            throw new ValidationException("AssetId must identify an asset in the specified branch.");
        if (request.SeverityId is long severityId && !await _dbContext.ReferenceValues.AsNoTracking()
            .AnyAsync(x => x.ReferenceId == severityId && x.GroupCode == "SEVERITY" && x.IsActive, cancellationToken))
            throw new ValidationException("SeverityId must identify an active SEVERITY reference value.");
        if (request.PriorityId is long priorityId && !await _dbContext.ReferenceValues.AsNoTracking()
            .AnyAsync(x => x.ReferenceId == priorityId && x.GroupCode == "PRIORITY" && x.IsActive, cancellationToken))
            throw new ValidationException("PriorityId must identify an active PRIORITY reference value.");
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
