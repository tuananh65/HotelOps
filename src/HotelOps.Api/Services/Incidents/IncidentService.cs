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

    public async Task<IncidentDetailDto?> ChangeIncidentStatusAsync(long id, ChangeIncidentStatusRequest request, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var incident = await _dbContext.Incidents
            .FromSqlInterpolated($"SELECT * FROM incidents WITH (UPDLOCK, HOLDLOCK) WHERE incident_id = {id}")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (incident is null)
        {
            return null;
        }

        if (!await _dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.UserId == request.ActorUserId, cancellationToken))
            throw new ValidationException("ActorUserId does not exist.");

        var status = request.Status.Trim().ToUpperInvariant();
        if (status is not ("OPEN" or "IN_PROGRESS" or "RESOLVED" or "CLOSED" or "CANCELLED"))
            throw new ValidationException("Status must be OPEN, IN_PROGRESS, RESOLVED, CLOSED, or CANCELLED.");

        if (incident.Status != status)
        {
            // The Incident row is already locked: corrective creation takes the same
            // lock before linking a work, so this check cannot race with creation.
            await RequireGenericStatusOwnershipAsync(incident.IncidentId, cancellationToken);
            var allowed = (incident.Status, status) switch
            {
                ("OPEN", "IN_PROGRESS" or "CANCELLED") => true,
                ("IN_PROGRESS", "RESOLVED" or "CANCELLED") => true,
                ("RESOLVED", "CLOSED" or "IN_PROGRESS") => true,
                _ => false
            };
            if (!allowed)
                throw new ValidationException($"Transition from {incident.Status} to {status} is not allowed.");

            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            if (status is "CLOSED" or "CANCELLED" && note is null)
                throw new ValidationException("Note is required when closing or cancelling an incident.");
            if (incident.Status == "RESOLVED" && status == "IN_PROGRESS" && note is null)
                throw new ValidationException("A reason is required when returning a resolved incident for rework.");

            var occurredAt = DateTime.UtcNow;
            var previousStatus = incident.Status;
            var previousResolvedAt = incident.ResolvedAt;
            var previousClosedAt = incident.ClosedAt;
            var previousClosureNote = incident.ClosureNote;
            var oldValues = JsonSerializer.Serialize(new
            {
                status = previousStatus,
                resolved_at = previousResolvedAt,
                closed_at = previousClosedAt,
                closure_note = previousClosureNote
            });

            incident.Status = status;
            if (status == "RESOLVED")
            {
                incident.ResolvedAt = occurredAt;
            }
            else if (previousStatus == "RESOLVED" && status == "IN_PROGRESS")
            {
                incident.ResolvedAt = null;
            }
            else if (status == "CLOSED")
            {
                incident.ClosedAt = occurredAt;
                incident.ClosureNote = note;
            }

            var changedFields = new List<string> { "status" };
            if (incident.ResolvedAt != previousResolvedAt)
                changedFields.Add("resolved_at");
            if (incident.ClosedAt != previousClosedAt)
                changedFields.Add("closed_at");
            if (incident.ClosureNote != previousClosureNote)
                changedFields.Add("closure_note");

            _dbContext.IncidentEvents.Add(new IncidentEvent
            {
                IncidentId = incident.IncidentId,
                EventType = "STATUS_CHANGED",
                FromStatus = previousStatus,
                ToStatus = status,
                ActorId = request.ActorUserId,
                Note = note,
                OccurredAt = occurredAt
            });
            _dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = "incidents",
                EntityId = incident.IncidentId.ToString(CultureInfo.InvariantCulture),
                Action = "UPDATE",
                ActorUserId = request.ActorUserId,
                BranchId = incident.BranchId,
                OccurredAt = occurredAt,
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new
                {
                    status = incident.Status,
                    resolved_at = incident.ResolvedAt,
                    closed_at = incident.ClosedAt,
                    closure_note = incident.ClosureNote
                }),
                ChangedFields = JsonSerializer.Serialize(changedFields)
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = await GetIncidentAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Incident could not be read after status operation.");
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task RequireGenericStatusOwnershipAsync(long incidentId, CancellationToken cancellationToken)
    {
        if (await _dbContext.MaintenanceWorks.AsNoTracking()
            .AnyAsync(work => work.IncidentId == incidentId, cancellationToken))
            throw new ValidationException("An incident with linked maintenance work must change status through the corrective Maintenance workflow.");
    }

    public async Task<IncidentDetailDto?> TriageIncidentAsync(long id, TriageIncidentRequest request, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Hold the target row's update lock through validation, change detection, and commit.
        var incident = await _dbContext.Incidents
            .FromSqlInterpolated($"SELECT * FROM incidents WITH (UPDLOCK, HOLDLOCK) WHERE incident_id = {id}")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (incident is null)
        {
            return null;
        }

        if (!await _dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.UserId == request.ActorUserId, cancellationToken))
            throw new ValidationException("ActorUserId does not exist.");
        if (incident.Status is not ("OPEN" or "IN_PROGRESS"))
            throw new ValidationException("Triage is allowed only for OPEN or IN_PROGRESS incidents.");

        var severityChanged = incident.SeverityId != request.SeverityId;
        var priorityChanged = incident.PriorityId != request.PriorityId;
        if (severityChanged && !await _dbContext.ReferenceValues.AsNoTracking()
            .AnyAsync(value => value.ReferenceId == request.SeverityId &&
                value.GroupCode == "SEVERITY" && value.IsActive, cancellationToken))
            throw new ValidationException("SeverityId must identify an active SEVERITY reference value.");
        if (priorityChanged && !await _dbContext.ReferenceValues.AsNoTracking()
            .AnyAsync(value => value.ReferenceId == request.PriorityId &&
                value.GroupCode == "PRIORITY" && value.IsActive, cancellationToken))
            throw new ValidationException("PriorityId must identify an active PRIORITY reference value.");

        if (severityChanged || priorityChanged)
        {
            var occurredAt = DateTime.UtcNow;
            var oldValues = JsonSerializer.Serialize(new
            {
                severity_id = incident.SeverityId,
                priority_id = incident.PriorityId
            });
            var changedFields = new List<string>();
            if (severityChanged)
                changedFields.Add("severity_id");
            if (priorityChanged)
                changedFields.Add("priority_id");

            incident.SeverityId = request.SeverityId!.Value;
            incident.PriorityId = request.PriorityId!.Value;
            _dbContext.IncidentEvents.Add(new IncidentEvent
            {
                IncidentId = incident.IncidentId,
                EventType = "TRIAGED",
                FromStatus = null,
                ToStatus = null,
                ActorId = request.ActorUserId,
                Note = null,
                OccurredAt = occurredAt
            });
            _dbContext.AuditLogs.Add(new AuditLog
            {
                BranchId = incident.BranchId,
                ActorUserId = request.ActorUserId,
                EntityType = "incidents",
                EntityId = incident.IncidentId.ToString(CultureInfo.InvariantCulture),
                Action = "UPDATE",
                OldValues = oldValues,
                NewValues = JsonSerializer.Serialize(new
                {
                    severity_id = incident.SeverityId,
                    priority_id = incident.PriorityId
                }),
                ChangedFields = JsonSerializer.Serialize(changedFields),
                OccurredAt = occurredAt
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = await GetIncidentAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Triaged incident could not be read.");
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IncidentDetailDto> CreateIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        await ValidateCreateAsync(request, cancellationToken);

        var reportedAt = DateTime.UtcNow;
        var prefix = "INC-" + reportedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialize allocation across application instances until this transaction ends.
        // Asset -> Incident number allocation. Retirement and handover writers also
        // lock Asset first; never allocate an Incident number while waiting for Asset.
        if (request.AssetId is long assetId)
        {
            var asset = await _dbContext.Assets
                .FromSqlInterpolated($"SELECT * FROM assets WITH (UPDLOCK, HOLDLOCK) WHERE asset_id = {assetId}")
                .AsTracking().SingleOrDefaultAsync(cancellationToken);
            if (asset is null || asset.BranchId != request.BranchId)
                throw new ValidationException("AssetId must identify an asset in the specified branch.");
            if (asset.LifecycleStatus == "RETIRED")
                throw new ValidationException("A retired Asset cannot receive a new Incident.");
        }

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

    public async Task<IncidentPagedResponse> GetIncidentsAsync(IncidentQueryRequest request, CancellationToken cancellationToken)
    {
        if (request.Page <= 0)
            throw new ValidationException("Page must be greater than zero.");
        if (request.PageSize <= 0 || request.PageSize > 100)
            throw new ValidationException("PageSize must be between 1 and 100.");

        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue)
            throw new ValidationException("Pagination offset exceeds the supported range.");

        var status = request.Status?.Trim();
        if (status is { Length: > 40 })
            throw new ValidationException("Status must not exceed 40 characters.");
        status = status?.ToUpperInvariant();
        if (!string.IsNullOrEmpty(status) &&
            status is not ("OPEN" or "IN_PROGRESS" or "RESOLVED" or "CLOSED" or "CANCELLED"))
            throw new ValidationException("Status must be OPEN, IN_PROGRESS, RESOLVED, CLOSED, or CANCELLED.");

        var query = _dbContext.Incidents.AsNoTracking();
        if (request.BranchId is long branchId)
            query = query.Where(incident => incident.BranchId == branchId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(incident => incident.Status == status);
        if (request.PriorityId is long priorityId)
            query = query.Where(incident => incident.PriorityId == priorityId);
        if (request.SeverityId is long severityId)
            query = query.Where(incident => incident.SeverityId == severityId);
        if (request.RoomId is long roomId)
            query = query.Where(incident => incident.RoomId == roomId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(incident => incident.ReportedAt)
            .ThenByDescending(incident => incident.IncidentId)
            .Skip((int)offset)
            .Take(request.PageSize)
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

        return new IncidentPagedResponse
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)(((long)totalCount + request.PageSize - 1) / request.PageSize)
        };
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
