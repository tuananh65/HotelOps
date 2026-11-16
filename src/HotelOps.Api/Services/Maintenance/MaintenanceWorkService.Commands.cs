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
        return ExecuteCorrectiveAsync(id, request, null, (work, incident, now, token) =>
        {
            RequireState(work, "PENDING");
            var end = request.PlannedEndAt ?? work.PlannedEndAt;
            ValidatePlannedTimes(work.PlannedStartAt, end);
            work.AssigneeId = request.AssigneeId;
            if (request.Instruction is not null)
                work.Instruction = request.Instruction;
            work.PlannedEndAt = end;
            return Task.CompletedTask;
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
        if (request is SubmitMaintenanceResultRequest)
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
        if (request is StartMaintenanceWorkRequest or ResumeMaintenanceWorkRequest or HoldMaintenanceWorkRequest or SubmitMaintenanceResultRequest)
            await MaintenanceExecutionVersion.RequireAsync(_dbContext, id, _authorityHttp?.HttpContext?.Request.Headers.IfMatch.ToString(), cancellationToken);
        await RequireUserAsync(TrustedActorId, "ActorUserId", cancellationToken);
        var resolutionMaterials = request is SubmitMaintenanceResultRequest
            ? await ValidateResolutionMaterialsAsync(work, incident, cancellationToken) : null;
        var resolutionEvidence = request is SubmitMaintenanceResultRequest submission
            ? await ValidateResolutionEvidenceAsync(submission, incident, cancellationToken) : null;

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
        if (request is SubmitMaintenanceResultRequest resultInput)
        {
            beforeWork["submitted_diagnosis"] = null;
            beforeWork["submitted_action_taken"] = null;
            beforeWork["submitted_materials"] = null;
            beforeWork["submitted_evidence"] = null;
            afterWork["submitted_diagnosis"] = resultInput.Diagnosis.Trim();
            afterWork["submitted_action_taken"] = resultInput.ActionTaken.Trim();
            afterWork["submitted_materials"] = resolutionMaterials;
            afterWork["submitted_evidence"] = resolutionEvidence;
        }
        AddAudit("maintenance_works", id, incident.BranchId, TrustedActorId,
            now, beforeWork, afterWork);
        if (incident.Status != previousStatus)
        {
            _dbContext.IncidentEvents.Add(new IncidentEvent
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
        var result = request is StartMaintenanceWorkRequest or ResumeMaintenanceWorkRequest or HoldMaintenanceWorkRequest or SubmitMaintenanceResultRequest
            ? await DetailAsync((await MaintenanceReadAccess.ReadAsync(_dbContext, TrustedActorId, incident.BranchId, cancellationToken))
                .Apply(_dbContext.MaintenanceWorks.AsNoTracking()), id, cancellationToken)
                ?? throw new Dev2AccessDenied(403)
            : await RequireDetailAsync(id, cancellationToken);
        if (request is StartMaintenanceWorkRequest or ResumeMaintenanceWorkRequest or HoldMaintenanceWorkRequest or SubmitMaintenanceResultRequest)
            result.ExecutionETag = await MaintenanceExecutionVersion.TagAsync(_dbContext, id, cancellationToken);
        if (request is SubmitMaintenanceResultRequest finalSubmission && resolutionEvidence is { Count: > 0 }
            && !resolutionEvidence.SequenceEqual(await ValidateResolutionEvidenceAsync(finalSubmission, incident, cancellationToken)))
            throw new ValidationException("Result evidence changed during submission. Reload before retrying.");
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
