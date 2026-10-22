using HotelOps.Api.DTOs.Maintenance;
using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    public Task<MaintenanceWorkDetailDto?> CancelWorkAsync(long id,
        CancelMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        var reason = RequiredNote(request.Reason, "Reason", 10);
        return ExecuteCorrectiveAsync(id, request, reason, (work, incident, now, token) =>
        {
            RequireState(work, "PENDING", "IN_PROGRESS", "ON_HOLD");
            work.Status = "CANCELLED";
            incident.Status = "CANCELLED";
            var log = AddWorkLog(work, request.ActorUserId, reason, 0, now);
            // Cancellation is recorded by Work/Incident history; the log CHECK
            // permits only execution statuses or NULL, not CANCELLED.
            log.Status = null;
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<MaintenanceWorkDetailDto?> VerifyWorkAsync(long id,
        VerifyMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        var note = RequiredNote(request.Note, "Note", 10);
        return ExecuteCorrectiveAsync(id, request, note, (work, incident, now, token) =>
        {
            RequireState(work, "COMPLETED");
            if (work.AssigneeId == request.ActorUserId)
                throw new ValidationException("The current assignee cannot verify their own work.");
            work.Status = "CLOSED";
            work.ReviewedBy = request.ActorUserId;
            work.ReviewedAt = now;
            incident.Status = "CLOSED";
            incident.ClosedAt = now;
            incident.ClosureNote = note;
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<MaintenanceWorkDetailDto?> ReturnWorkAsync(long id,
        ReturnMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        var reason = RequiredNote(request.Reason, "Reason", 10);
        return ExecuteCorrectiveAsync(id, request, reason, (work, incident, now, token) =>
        {
            RequireState(work, "COMPLETED");
            if (work.AssigneeId == request.ActorUserId)
                throw new ValidationException("The current assignee cannot return their own work for rework.");
            work.Status = "IN_PROGRESS";
            work.ResolvedAt = null;
            work.ResolutionNote = null;
            work.ReviewedBy = null;
            work.ReviewedAt = null;
            incident.Status = "IN_PROGRESS";
            incident.ResolvedAt = null;
            AddWorkLog(work, request.ActorUserId, reason, 0, now);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<MaintenanceWorkDetailDto?> SubmitResultAsync(long id,
        SubmitMaintenanceResultRequest request, CancellationToken cancellationToken)
    {
        var note = RequiredNote(request.ResolutionNote, "ResolutionNote");
        return ExecuteCorrectiveAsync(id, request, note, (work, incident, now, token) =>
        {
            RequireState(work, "IN_PROGRESS");
            RequireAssignee(work, request.ActorUserId);
            work.Status = "COMPLETED";
            work.ResolvedAt = now;
            work.ResolutionNote = note;
            incident.Status = "RESOLVED";
            incident.ResolvedAt = now;
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<MaintenanceWorkDetailDto?> StartWorkAsync(long id,
        StartMaintenanceWorkRequest request, CancellationToken cancellationToken) =>
        ExecuteCorrectiveAsync(id, request, null, (work, incident, now, token) =>
        {
            RequireState(work, "PENDING");
            RequireAssignee(work, request.ActorUserId);
            work.Status = "IN_PROGRESS";
            work.ActualStartAt = now;
            incident.Status = "IN_PROGRESS";
            return Task.CompletedTask;
        }, cancellationToken);
}
