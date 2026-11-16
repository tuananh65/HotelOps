using HotelOps.Api.Services.Dev2Authorization;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    public Task<MaintenanceWorkDetailDto?> AssignWorkAsync(long id,
        AssignMaintenanceWorkRequest request, CancellationToken cancellationToken) {
        Dev2ServiceAuthority.RequireEntry(_authorityHttp, "MaintenanceWorks", ["AssignWork"], request.ActorUserId, id, request);
        return ExecuteCorrectiveAsync(id, request, null, async (work, incident, now, token) =>
        {
            RequireState(work, "PENDING");
            if (work.AssigneeId == request.AssigneeId)
                throw new ValidationException("The selected technician is already the current assignee.");
            if (await MaintenanceAssignmentProof.ReadAsync(_dbContext, id, token) is not ("ASSIGNED" or "PENDING_ASSIGNMENT"))
                throw new ValidationException("Verified Triage and unstarted Pending Assignment/Assigned work required.");
            await MaintenanceAssignmentEligibility.RequireAsync(_dbContext, request.AssigneeId, incident.BranchId, id, token);
            var end = request.PlannedEndAt ?? work.PlannedEndAt;
            MaintenanceAssignmentEligibility.RequirePlannedEnd(work.PlannedStartAt, request.PlannedEndAt
                ?? (end is {} stored ? DateTime.SpecifyKind(stored, DateTimeKind.Utc) : null));
            work.AssigneeId = request.AssigneeId;
            if (request.Instruction is not null)
                work.Instruction = request.Instruction;
            work.PlannedEndAt = end;
        }, cancellationToken, prepareOwners: token =>
            new HotelOps.Api.Services.Users.UserEligibilityLock(_dbContext).RequireEligibleAsync(request.AssigneeId, token));
    }

    private async Task<MaintenanceWorkDetailDto?> ExecuteCorrectiveCoreAsync(long id,
        MaintenanceActorRequest request, string? incidentNote,
        Func<MaintenanceWork, Incident, DateTime, CancellationToken, Task> operation,
        CancellationToken cancellationToken, Func<CancellationToken, Task>? prepareOwners = null,
        bool restoreAssigneeResponsibility = false)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        var discovered = await _dbContext.MaintenanceWorks.AsNoTracking()
            .Where(work => work.MaintenanceWorkId == id)
            .Select(work => new { work.MaintenanceWorkId, work.IncidentId, work.AssigneeId, work.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (discovered is null)
            return null;
        if (discovered.IncidentId is not long incidentId)
            throw new ValidationException("This action requires a corrective work linked to an incident.");

        await using var transaction = await Dev2ServiceAuthority.BeginTransactionAsync(_dbContext, _authorityHttp, cancellationToken);
        if (request is VerifyMaintenanceWorkRequest or CancelMaintenanceWorkRequest
            && !await HotelOps.Api.Services.Incidents.IncidentEscalationClosure.InstalledAsync(_dbContext, cancellationToken))
            throw new Dev2AccessDenied(503);
        if (request is SubmitMaintenanceResultRequest or VerifyMaintenanceWorkRequest)
            await LockResolutionMaterialsAsync(id, cancellationToken);
        HotelOps.Api.Services.WorkTime.SqlWorkTimeTransactionParticipant? workTime = null;
        if (OpensWorkInterval(request) || ClosesWorkInterval(request))
        {
            workTime = new(_dbContext);
            await workTime.LockUsersAsync(discovered.AssigneeId is {} owner
                ? [TrustedActorId, owner] : [TrustedActorId], cancellationToken);
        }
        if (restoreAssigneeResponsibility)
        {
            if (discovered.AssigneeId is not long assigneeId)
                throw new ValidationException("Returning work requires a current assignee.");
            await new HotelOps.Api.Services.Users.UserEligibilityLock(_dbContext)
                .RequireEligibleAsync(assigneeId, cancellationToken);
        }
        if (prepareOwners is not null) await prepareOwners(cancellationToken);
        // Responsibility User locks, when required, precede Incident -> MaintenanceWork.
        // All locks are retained until this transaction commits or rolls back.
        var incident = await LockIncidentAsync(incidentId, cancellationToken);
        var work = await _dbContext.MaintenanceWorks
            .FromSqlInterpolated($"SELECT * FROM maintenance_works WITH (UPDLOCK, HOLDLOCK) WHERE maintenance_work_id = {id}")
            .AsTracking().SingleOrDefaultAsync(cancellationToken);
        if (work is null)
            return null;
        // Tracking queries can retain state from before the User-lock wait.
        await _dbContext.Entry(work).ReloadAsync(cancellationToken);
        if (incident is not null) await _dbContext.Entry(incident).ReloadAsync(cancellationToken);
        if (incident is null || work.IncidentId != incidentId)
            throw new ValidationException("The corrective work relationship changed. Reload before retrying.");
        if (restoreAssigneeResponsibility &&
            (work.AssigneeId != discovered.AssigneeId || work.Status != discovered.Status))
            throw new ValidationException("The maintenance assignee or state changed after discovery. Reload before retrying.");
        if (workTime is not null && work.AssigneeId != discovered.AssigneeId)
            throw new ValidationException("The maintenance assignee changed after time-lock discovery. Reload before retrying.");
        RequireSynchronizedPair(work, incident);
        if (request is AssignMaintenanceWorkRequest or StartMaintenanceWorkRequest or ResumeMaintenanceWorkRequest or HoldMaintenanceWorkRequest or SubmitMaintenanceResultRequest or CreateMaintenanceWorkLogRequest or VerifyMaintenanceWorkRequest or ReturnMaintenanceWorkRequest)
            await MaintenanceExecutionVersion.RequireAsync(_dbContext, id, _authorityHttp?.HttpContext?.Request.Headers.IfMatch.ToString(), cancellationToken);
        await RequireUserAsync(TrustedActorId, "ActorUserId", cancellationToken);
        var returnedExecution = request is ResumeMaintenanceWorkRequest && work.Status == "IN_PROGRESS"
            ? await MaintenanceReturnedExecution.ReadAsync(_dbContext, id, TrustedActorId, incident.BranchId, cancellationToken)
                ?? throw new ValidationException("Current unused Return evidence is required to restart this work.")
            : null;
        var resolutionMaterials = request is SubmitMaintenanceResultRequest
            ? await ValidateResolutionMaterialsAsync(work, incident, cancellationToken) : null;
        var resolutionEvidence = request is SubmitMaintenanceResultRequest submission
            ? await ValidateResolutionEvidenceAsync(submission, incident, cancellationToken) : null;
        var reviewedSubmission = request is VerifyMaintenanceWorkRequest reviewRequest
            ? await RequireReviewProofAsync(work, incident, reviewRequest.SubmissionAuditId, cancellationToken) : null;

        var beforeWork = WorkSnapshot(work);
        var beforeIncident = IncidentSnapshot(incident);
        var previousStatus = incident.Status;
        var now = DateTime.SpecifyKind(await _dbContext.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value")
            .SingleAsync(cancellationToken), DateTimeKind.Utc);
        if (workTime is not null && ClosesWorkInterval(request))
            now = await CloseWorkIntervalAsync(workTime, work, incident, cancellationToken) ?? now;
        await operation(work, incident, now, cancellationToken);
        if (workTime is not null && OpensWorkInterval(request))
        {
            now = await OpenWorkIntervalAsync(workTime, work, incident, cancellationToken);
            if (request is StartMaintenanceWorkRequest) work.ActualStartAt = now;
        }
        var afterWork = WorkSnapshot(work);
        if (request is VerifyMaintenanceWorkRequest verified)
        {
            foreach (var key in new[] { "notification_kind", "notification_version", "closure_reporter_id", "verification_note" })
                beforeWork[key] = null;
            afterWork["notification_kind"] = "MAINTENANCE_VERIFIED";
            afterWork["notification_version"] = 1;
            afterWork["closure_reporter_id"] = incident.ReportedBy;
            afterWork["verification_note"] = verified.Note.Trim();
            afterWork["verified_submission_audit_id"] = reviewedSubmission!.Review.SubmissionAuditId;
            // Bind historical SLA to this committed closure, before a future Reopen
            // replaces the Incident's current-cycle deadlines. Never use planned dates.
            afterWork["incident_cycle_sla_version"] = 1;
            afterWork["incident_cycle_discovered_at"] = incident.DiscoveredAt;
            afterWork["incident_cycle_sla"] = await HotelOps.Api.Services.Incidents.IncidentSlaRead.ReadAsync(
                _dbContext, incident.IncidentId, cancellationToken);
            if (incident.AssetId is long assetId)
            {
                var asset = await _dbContext.Assets.AsNoTracking().SingleOrDefaultAsync(a => a.AssetId == assetId
                    && a.BranchId == incident.BranchId, cancellationToken);
                if (asset == null) throw new ValidationException("Incident asset branch is inconsistent.");
                if (asset.ConditionStatus is "DAMAGED" or "UNDER_REPAIR")
                {
                    afterWork["closure_asset_id"] = asset.AssetId;
                    afterWork["closure_asset_condition"] = asset.ConditionStatus;
                }
            }
        }
        if (returnedExecution != null)
        {
            // Opening a new interval while physical state stays IN_PROGRESS must still
            // consume the owning version, so a concurrent/replayed Resume is stale.
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.maintenance_works SET status=status WHERE maintenance_work_id={id}", cancellationToken);
            beforeWork["resumed_return_audit_id"] = null;
            beforeWork["resumed_interval_opened_at"] = null;
            afterWork["resumed_return_audit_id"] = returnedExecution.AuditId;
            afterWork["resumed_interval_opened_at"] = now;
        }
        if (request is ReturnMaintenanceWorkRequest returned)
        {
            beforeWork["notification_kind"] = null;
            beforeWork["notification_version"] = null;
            beforeWork["return_reason"] = null;
            afterWork["notification_kind"] = "MAINTENANCE_RETURNED";
            afterWork["notification_version"] = 1;
            afterWork["return_reason"] = returned.Reason.Trim();
        }
        if (request is SubmitMaintenanceResultRequest resultInput)
        {
            beforeWork["submitted_diagnosis"] = null;
            beforeWork["submitted_action_taken"] = null;
            beforeWork["submitted_materials"] = null;
            beforeWork["submitted_evidence"] = null;
            beforeWork["notification_kind"] = null;
            beforeWork["notification_version"] = null;
            afterWork["submitted_diagnosis"] = resultInput.Diagnosis.Trim();
            afterWork["submitted_action_taken"] = resultInput.ActionTaken.Trim();
            afterWork["submitted_materials"] = resolutionMaterials;
            afterWork["submitted_evidence"] = resolutionEvidence;
            afterWork["notification_kind"] = "MAINTENANCE_SUBMITTED";
            afterWork["notification_version"] = 1;
        }
        AddAudit("maintenance_works", id, incident.BranchId, TrustedActorId,
            now, beforeWork, afterWork);
        IncidentEvent? transitionEvent = null;
        if (incident.Status != previousStatus)
        {
            _dbContext.IncidentEvents.Add(transitionEvent = new IncidentEvent
            {
                IncidentId = incident.IncidentId,
                EventType = "STATUS_CHANGED",
                FromStatus = previousStatus,
                ToStatus = incident.Status,
                ActorId = TrustedActorId,
                Note = incidentNote,
                OccurredAt = now
            });
            AddAudit("incidents", incident.IncidentId, incident.BranchId, TrustedActorId,
                now, beforeIncident, IncidentSnapshot(incident));
        }

        if (_dbContext.ChangeTracker.HasChanges())
            await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        if (request is VerifyMaintenanceWorkRequest or CancelMaintenanceWorkRequest)
        {
            if (transitionEvent == null) throw new Dev2AccessDenied(409);
            await Dev2ServiceAuthority.RevalidateAsync(_dbContext, _authorityHttp, Dev2AuthorizationPhase.BeforePersistence, cancellationToken);
            var closed = await HotelOps.Api.Services.Incidents.IncidentEscalationClosure.CloseAsync(_dbContext,
                incident.IncidentId, incident.BranchId, TrustedActorId, transitionEvent.EventId, now, cancellationToken);
            foreach (var escalation in closed)
            {
                var old = new Dictionary<string, object?> { ["incident_id"] = incident.IncidentId,
                    ["state"] = "OPEN", ["opened_event_id"] = escalation.OpenedEventId, ["opened_at"] = escalation.OpenedAt,
                    ["closed_event_id"] = null, ["closed_at"] = null };
                var updated = new Dictionary<string, object?>(old) { ["state"] = "CLOSED",
                    ["closed_event_id"] = transitionEvent.EventId, ["closed_at"] = now };
                AddAudit("incident_escalations", escalation.EscalationId, incident.BranchId, TrustedActorId, now, old, updated);
            }
            if (closed.Length > 0) await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, cancellationToken);
        }
        var result = request is AssignMaintenanceWorkRequest or StartMaintenanceWorkRequest or ResumeMaintenanceWorkRequest or HoldMaintenanceWorkRequest or SubmitMaintenanceResultRequest or CreateMaintenanceWorkLogRequest or VerifyMaintenanceWorkRequest or ReturnMaintenanceWorkRequest
            ? await DetailAsync((await MaintenanceReadAccess.ReadAsync(_dbContext, TrustedActorId, incident.BranchId, cancellationToken))
                .Apply(_dbContext.MaintenanceWorks.AsNoTracking()), id, cancellationToken)
                ?? throw new Dev2AccessDenied(403)
            : await RequireDetailAsync(id, cancellationToken);
        if (request is AssignMaintenanceWorkRequest or StartMaintenanceWorkRequest or ResumeMaintenanceWorkRequest or HoldMaintenanceWorkRequest or SubmitMaintenanceResultRequest or CreateMaintenanceWorkLogRequest or VerifyMaintenanceWorkRequest or ReturnMaintenanceWorkRequest)
            result.ExecutionETag = await MaintenanceExecutionVersion.TagAsync(_dbContext, id, cancellationToken);
        if (request is SubmitMaintenanceResultRequest finalSubmission && resolutionEvidence is { Count: > 0 }
            && !resolutionEvidence.SequenceEqual(await ValidateResolutionEvidenceAsync(finalSubmission, incident, cancellationToken)))
            throw new ValidationException("Result evidence changed during submission. Reload before retrying.");
        if (request is SubmitMaintenanceResultRequest)
        {
            result.SubmissionWorkflow = await MaintenanceSubmissionWorkflow.ReadAsync(_dbContext,id,cancellationToken);
            if (result.SubmissionWorkflow != "PENDING_VERIFICATION")
                throw new ValidationException("Committed submission evidence and closed interval are required.");
        }
        if (reviewedSubmission != null)
            await RequireReviewEvidenceAsync(reviewedSubmission.Review, reviewedSubmission.Evidence, incident, cancellationToken);
        await Dev2ServiceAuthority.CommitAsync(_dbContext, _authorityHttp, transaction, cancellationToken);
        return result;
    }

    private static void RequireSynchronizedPair(MaintenanceWork work, Incident incident)
    {
        var expected = work.Status switch
        {
            "PENDING" => "OPEN",
            "IN_PROGRESS" or "ON_HOLD" => "IN_PROGRESS",
            "COMPLETED" => "RESOLVED",
            "CLOSED" => "CLOSED",
            "CANCELLED" => "CANCELLED",
            _ => null
        };
        if (expected is null || incident.Status != expected)
            throw new ValidationException("Maintenance and Incident statuses are inconsistent. No automatic repair is performed.");
    }

    private static void RequireState(MaintenanceWork work, params string[] allowed)
    {
        if (!allowed.Contains(work.Status))
            throw new ValidationException("This action is not allowed in the current maintenance status.");
    }

    private static void RequireAssignee(MaintenanceWork work, long actorId)
    {
        if (work.AssigneeId is null || work.AssigneeId != actorId)
            throw new ValidationException("Only the current assignee can perform this action.");
    }

    private Task<Incident?> LockIncidentAsync(long id, CancellationToken cancellationToken) =>
        _dbContext.Incidents
            .FromSqlInterpolated($"SELECT * FROM incidents WITH (UPDLOCK, HOLDLOCK) WHERE incident_id = {id}")
            .AsTracking().SingleOrDefaultAsync(cancellationToken);

    private async Task RequireUserAsync(long id, string field, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Users.AsNoTracking().AnyAsync(user => user.UserId == id, cancellationToken))
            throw new ValidationException(field + " does not exist.");
    }

    private static void ValidatePlannedTimes(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue && end.Value < start.Value)
            throw new ValidationException("PlannedEndAt must not precede PlannedStartAt.");
    }

    private async Task<MaintenanceWorkDetailDto> RequireDetailAsync(long id, CancellationToken cancellationToken) =>
        await GetWorkAsync(id, cancellationToken)
        ?? throw new InvalidOperationException("Maintenance work could not be read.");
}
