using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Incidents;
using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Entities;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace HotelOps.Api.Services.Incidents;

public sealed class IncidentService : IIncidentService
{
    private readonly IHttpContextAccessor? _authorityHttp;
    private long TrustedActorId => Dev2ServiceAuthority.ActorId(_authorityHttp);

    private readonly HotelOpsDbContext _dbContext;

    public IncidentService(HotelOpsDbContext dbContext, IHttpContextAccessor? authorityHttp = null)
    {
        _dbContext = dbContext;
        _authorityHttp = authorityHttp;
    }

    public async Task<IncidentDetailDto?> ChangeIncidentStatusAsync(long id, ChangeIncidentStatusRequest request, CancellationToken cancellationToken)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Incidents", ["ChangeIncidentStatus"], request.ActorUserId, id, request);
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);
        var incident = await _dbContext.Incidents
            .FromSqlInterpolated($"SELECT * FROM incidents WITH (UPDLOCK, HOLDLOCK) WHERE incident_id = {id}")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (incident is null)
        {
            return null;
        }

        if (!await _dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.UserId == TrustedActorId, cancellationToken))
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
                ActorId = TrustedActorId,
                Note = note,
                OccurredAt = occurredAt
            });
            _dbContext.AuditLogs.Add(new AuditLog
            {
                EntityType = "incidents",
                EntityId = incident.IncidentId.ToString(CultureInfo.InvariantCulture),
                Action = "UPDATE",
                ActorUserId = TrustedActorId,
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
            await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        }

        var result = await GetIncidentAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Incident could not be read after status operation.");
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
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
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Incidents", ["TriageIncident"], request.ActorUserId, id, request);
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);
        var incident = await _dbContext.Incidents
            .FromSqlInterpolated($"SELECT * FROM dbo.incidents WITH(UPDLOCK,HOLDLOCK) WHERE incident_id={id}")
            .AsTracking().SingleOrDefaultAsync(cancellationToken);
        if (incident == null) return null;
        await _dbContext.Entry(incident).ReloadAsync(cancellationToken);
        var currentTag = await IncidentTriageStore.TagAsync(_dbContext, id, cancellationToken) ?? throw new Dev2AccessDenied(503);
        var expectedTag = _authorityHttp?.HttpContext?.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(expectedTag)) throw new Dev2AccessDenied(428);
        if (expectedTag != currentTag) throw new Dev2AccessDenied(412);
        if (await IncidentAssignmentWorkflow.ReadAsync(_dbContext, incident, cancellationToken) != "PENDING_TRIAGE")
            throw new ValidationException("Triage requires verified Pending Triage without assignment or execution in the current cycle.");
        var cycle = await IncidentReopenStore.ReadAsync(_dbContext, id, cancellationToken);
        var cycleDiscovery = cycle?.DiscoveredAt ?? incident.DiscoveredAt;
        if (request.SeverityId is not > 0 || request.PriorityId is not > 0 || incident.SeverityId != request.SeverityId
            || !await _dbContext.ReferenceValues.AsNoTracking().AnyAsync(r => r.ReferenceId == request.SeverityId
                && r.GroupCode == "SEVERITY" && r.IsActive, cancellationToken))
            throw new ValidationException("Active severity and priority references are required.");
        if (!await IncidentCategoryStore.InstalledAsync(_dbContext, cancellationToken)) throw new Dev2AccessDenied(503);
        if (!await IncidentCategoryStore.Rows(_dbContext).AnyAsync(r => r.IncidentId == id && r.CategoryId != null, cancellationToken))
            throw new ValidationException("Classify the Incident before Triage: category and severity are required.");
        var actionPlan = request.InitialActionPlan?.Trim();
        if (string.IsNullOrEmpty(actionPlan)) throw new ValidationException("Initial action plan is required.");
        var existing = await IncidentSlaRead.ReadAsync(_dbContext, id, cancellationToken);
        if (existing.UnavailableReason != "INCIDENT_SLA_NOT_RECORDED" || existing.ResponseDeadlineAt != null || existing.ResolutionDeadlineAt != null)
            throw new ValidationException("Initial Triage cannot replace existing or unavailable SLA evidence.");
        var target = await IncidentTriageStore.TargetAsync(_dbContext, request.PriorityId.Value, cancellationToken);
        if (target.Version != request.ExpectedSlaTargetVersion) throw new Dev2AccessDenied(412);
        var defaults = IncidentTriageStore.Calculate(cycleDiscovery, target, new());
        var deadlines = IncidentTriageStore.Calculate(cycleDiscovery, target, request);
        var now = await _dbContext.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(cancellationToken);
        var before = JsonSerializer.Serialize(new { severity_id = incident.SeverityId, priority_id = incident.PriorityId,
            response_deadline_at = (DateTime?)null, resolution_deadline_at = (DateTime?)null });
        incident.SeverityId = request.SeverityId.Value;
        incident.PriorityId = request.PriorityId.Value;
        _dbContext.IncidentEvents.Add(new IncidentEvent { IncidentId = id, EventType = "TRIAGED", FromStatus = "OPEN", ToStatus = "OPEN",
            ActorId = TrustedActorId, OccurredAt = now, Note = actionPlan });
        _dbContext.AuditLogs.Add(new AuditLog { BranchId = incident.BranchId, ActorUserId = TrustedActorId,
            EntityType = "incidents", EntityId = id.ToString(CultureInfo.InvariantCulture), Action = "UPDATE", OldValues = before,
            NewValues = JsonSerializer.Serialize(new { severity_id = incident.SeverityId, priority_id = incident.PriorityId,
                response_deadline_at = deadlines.Response, resolution_deadline_at = deadlines.Resolution,
                discovered_at = incident.DiscoveredAt, cycle_discovered_at = cycleDiscovery, reopen_event_id = cycle?.EventId,
                sla_response_minutes = target.ResponseMinutes,
                sla_resolution_minutes = target.ResolutionMinutes, sla_target_version = target.Version,
                default_response_deadline_at = defaults.Response, default_resolution_deadline_at = defaults.Resolution,
                initial_action_plan = actionPlan,
                deadline_change_reason = deadlines.OverrideReason, workflow_status = "PENDING_ASSIGNMENT" }),
            ChangedFields = JsonSerializer.Serialize(new[] { "severity_id", "priority_id", "response_deadline_at", "resolution_deadline_at" }), OccurredAt = now });
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        await IncidentTriageStore.WriteAsync(_dbContext, id, deadlines, cancellationToken);
        var result = await GetIncidentAsync(id, cancellationToken) ?? throw new InvalidOperationException("Triaged incident could not be read.");
        result.Sla = await IncidentSlaRead.ReadAsync(_dbContext, id, cancellationToken);
        result.TriageETag = await IncidentTriageStore.TagAsync(_dbContext, id, cancellationToken);
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
        return result;
    }

    public async Task<IncidentDetailDto> CreateIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "Incidents", ["CreateIncident"], request.ReportedBy, null, request);
        IncidentCreationInput.Validate(request);
        var identity = Dev2ServiceAuthority.CurrentIdentity(_authorityHttp);
        var services = _authorityHttp!.HttpContext!.RequestServices;
        IncidentReviewReceipt.Validate(services.GetRequiredService<IDataProtectionProvider>(), services.GetRequiredService<TimeProvider>(),
            identity.UserId, identity.SessionId, request);
        await ValidateCreateAsync(request, cancellationToken);

        var reportedAt = await _dbContext.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(cancellationToken);
        var prefix = "INC-" + reportedAt.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-";
        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);

        // Serialize allocation across application instances until this transaction ends.
        var requestLink = await IncidentRequestCreation.PrepareAsync(_dbContext, TrustedActorId, request, cancellationToken);
        await IncidentOccurrenceCreation.PrepareAsync(_dbContext, TrustedActorId, request, cancellationToken);
        await IncidentCategoryStore.ValidateAsync(_dbContext, request.CategoryId, cancellationToken);
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
            ReportedBy = TrustedActorId,
            IncidentTitle = request.IncidentTitle.Trim(),
            Description = request.Description.Trim(),
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
            ActorId = TrustedActorId,
            OccurredAt = reportedAt,
            Note = null
        });
        _dbContext.Incidents.Add(incident);
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        await IncidentCategoryStore.WriteAsync(_dbContext, incident.IncidentId, request.CategoryId, cancellationToken);
        if (request.Files is { Count: > 0 })
        {
            var root = services.GetRequiredService<IConfiguration>()["Evidence:PrivateRoot"]
                ?? Path.Combine(services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "App_Data", "evidence");
            var files = Dev2ServiceAuthority.OwnEvidence(_authorityHttp);
            await IncidentEvidenceWrite.StageFilesAsync(_dbContext, incident.IncidentId, TrustedActorId, reportedAt,
                request.Files, root, files, cancellationToken);
            await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        }

        _dbContext.AuditLogs.Add(new AuditLog
        {
            BranchId = incident.BranchId,
            ActorUserId = TrustedActorId,
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
                room_id = incident.RoomId,
                category_id = request.CategoryId,
                attachment_ids = _dbContext.Attachments.Local.Where(a => a.EntityType == "INCIDENT" && a.EntityId == incident.IncidentId)
                    .Select(a => a.AttachmentId).ToArray()
            })
        });
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        if (requestLink is not null)
            await IncidentRequestCreation.WriteAsync(_dbContext, requestLink, incident.IncidentId, cancellationToken);
        await IncidentOccurrenceCreation.WriteAsync(_dbContext, TrustedActorId, request, incident.IncidentId, cancellationToken);
        var result = await GetIncidentAsync(incident.IncidentId, cancellationToken)
            ?? throw new InvalidOperationException("Created incident could not be read.");
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
        return result;
    }

    private async Task ValidateCreateAsync(CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Branches.AsNoTracking().AnyAsync(x => x.BranchId == request.BranchId, cancellationToken))
            throw new ValidationException("BranchId does not exist.");
        if (!await _dbContext.Users.AsNoTracking().AnyAsync(x => x.UserId == TrustedActorId, cancellationToken))
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
        => await ListAsync(_dbContext.Incidents.AsNoTracking(), request, cancellationToken, _dbContext);

    internal static async Task<IncidentPagedResponse> ListAsync(IQueryable<Incident> query, IncidentQueryRequest request, CancellationToken cancellationToken, HotelOpsDbContext? db = null)
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

        if (request.BranchId is long branchId)
            query = query.Where(incident => incident.BranchId == branchId);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(incident => incident.Status == status);
        if (request.PriorityId is long priorityId)
            query = query.Where(incident => incident.PriorityId == priorityId);
        if (request.SeverityId is long severityId)
            query = query.Where(incident => incident.SeverityId == severityId);
        query = IncidentLocationQuery.Apply(query, request);
        if (request.BranchId is <= 0 || request.RoomId is <= 0 || request.AreaId is <= 0
            || request.AssetId is <= 0 || request.PriorityId is <= 0 || request.SeverityId is <= 0)
            throw new ValidationException("Filter identifiers must be positive when supplied.");
        if (request.DetectedFrom is {} from && request.DetectedTo is {} to && from > to)
            throw new ValidationException("DetectedFrom must not be later than DetectedTo.");
        if (request.DetectedFrom is {} detectedFrom)
        {
            var fromUtc = detectedFrom.UtcDateTime;
            query = query.Where(incident => incident.DiscoveredAt >= fromUtc);
        }
        if (request.DetectedTo is {} detectedTo)
        {
            var toUtc = detectedTo.UtcDateTime;
            query = query.Where(incident => incident.DiscoveredAt <= toUtc);
        }
        var keyword = request.Keyword?.Trim();
        if (keyword is { Length: > 200 }) throw new ValidationException("Keyword must not exceed 200 characters.");
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(incident => incident.IncidentNo.Contains(keyword)
                || incident.IncidentTitle.Contains(keyword) || incident.Description.Contains(keyword));

        if (request.CategoryId is <= 0) throw new ValidationException("CategoryId must be positive.");
        var categoryAvailable = db is not null && await IncidentCategoryStore.InstalledAsync(db, cancellationToken);
        if (request.CategoryId is long categoryId)
        {
            if (!categoryAvailable) throw new Dev2AccessDenied(503);
            var categorized = IncidentCategoryStore.Rows(db!).Where(c => c.CategoryId == categoryId).Select(c => c.IncidentId);
            query = query.Where(i => categorized.Contains(i.IncidentId));
        }
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

        if (categoryAvailable && items.Count > 0)
        {
            var ids = items.Select(i => i.IncidentId).ToArray();
            var categories = await IncidentCategoryStore.Rows(db!).Where(c => ids.Contains(c.IncidentId))
                .ToDictionaryAsync(c => c.IncidentId, cancellationToken);
            foreach (var item in items)
            {
                var category = categories.GetValueOrDefault(item.IncidentId);
                item.CategoryId = category?.CategoryId;
                item.CategoryCode = category?.CategoryCode;
                item.CategoryName = category?.CategoryName;
                item.CategoryUnavailableReason = category?.CategoryId is null ? "INCIDENT_CATEGORY_NOT_RECORDED" : null;
            }
        }
        else if (!categoryAvailable)
            foreach (var item in items) item.CategoryUnavailableReason = "INCIDENT_CATEGORY_STORAGE_UNAVAILABLE";

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
        => await DetailAsync(_dbContext.Incidents.AsNoTracking(), id, cancellationToken, _dbContext);

    internal static async Task<IncidentDetailDto?> DetailAsync(IQueryable<Incident> query, long id, CancellationToken cancellationToken, HotelOpsDbContext? db = null)
    {
        var detail = await query
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
        if (detail is not null && db is not null)
            await IncidentCategoryStore.EnrichAsync(db, detail, cancellationToken);
        return detail;
    }
}
