using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    public Task<MaintenanceWorkDetailDto?> AssignWorkAsync(long id,
        AssignMaintenanceWorkRequest request, CancellationToken cancellationToken) =>
        ExecuteCorrectiveAsync(id, request, null, async (work, incident, now, token) =>
        {
            RequireState(work, "PENDING");
            await RequireUserAsync(request.AssigneeId, "AssigneeId", token);
            var end = request.PlannedEndAt ?? work.PlannedEndAt;
            ValidatePlannedTimes(work.PlannedStartAt, end);
            work.AssigneeId = request.AssigneeId;
            if (request.Instruction is not null)
                work.Instruction = request.Instruction;
            work.PlannedEndAt = end;
        }, cancellationToken);

    private async Task<MaintenanceWorkDetailDto?> ExecuteCorrectiveAsync(long id,
        MaintenanceActorRequest request, string? incidentNote,
        Func<MaintenanceWork, Incident, DateTime, CancellationToken, Task> operation,
        CancellationToken cancellationToken, Func<CancellationToken, Task>? prepareOwners = null)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        var discovered = await _dbContext.MaintenanceWorks.AsNoTracking()
            .Where(work => work.MaintenanceWorkId == id)
            .Select(work => new { work.IncidentId })
            .SingleOrDefaultAsync(cancellationToken);
        if (discovered is null)
            return null;
        if (discovered.IncidentId is not long incidentId)
            throw new ValidationException("This action requires a corrective work linked to an incident.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (prepareOwners is not null) await prepareOwners(cancellationToken);
        // All corrective operations lock Incident first, then MaintenanceWork.
        var incident = await LockIncidentAsync(incidentId, cancellationToken);
        var work = await _dbContext.MaintenanceWorks
            .FromSqlInterpolated($"SELECT * FROM maintenance_works WITH (UPDLOCK, HOLDLOCK) WHERE maintenance_work_id = {id}")
            .AsTracking().SingleOrDefaultAsync(cancellationToken);
        if (work is null)
            return null;
        if (incident is null || work.IncidentId != incidentId)
            throw new ValidationException("The corrective work relationship changed. Reload before retrying.");
        RequireSynchronizedPair(work, incident);
        await RequireUserAsync(request.ActorUserId, "ActorUserId", cancellationToken);
        var cycle = await CycleContext(work, incident, cancellationToken);
        ExpectedCycle(cycle, request.ExpectedCycleId, true);
        _logCycleId = cycle?.CycleId;

        var beforeWork = WorkSnapshot(work);
        var beforeIncident = IncidentSnapshot(incident);
        var previousStatus = incident.Status;
        var now = _clock.GetUtcNow().UtcDateTime;
        await operation(work, incident, now, cancellationToken);
        if (cycle is not null && work.Status is "CLOSED" or "CANCELLED")
            FinalizeCycle(cycle, work, incident, request.ActorUserId, incidentNote!, now);
        AddAudit("maintenance_works", id, incident.BranchId, request.ActorUserId,
            now, beforeWork, WorkSnapshot(work));
        if (incident.Status != previousStatus)
        {
            _dbContext.IncidentEvents.Add(new IncidentEvent
            {
                IncidentId = incident.IncidentId,
                EventType = "STATUS_CHANGED",
                FromStatus = previousStatus,
                ToStatus = incident.Status,
                ActorId = request.ActorUserId,
                Note = incidentNote,
                OccurredAt = now
            });
            AddAudit("incidents", incident.IncidentId, incident.BranchId, request.ActorUserId,
                now, beforeIncident, IncidentSnapshot(incident));
        }

        if (_dbContext.ChangeTracker.HasChanges())
            await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await RequireDetailAsync(id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
