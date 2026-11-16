using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    private async Task<MaintenanceWorkDetailDto?> ExecuteCorrectiveAsync(long id,
        MaintenanceActorRequest request, string? incidentNote,
        Func<MaintenanceWork, Incident, DateTime, CancellationToken, Task> operation,
        CancellationToken cancellationToken, Func<CancellationToken, Task>? prepareOwners = null,
        bool restoreAssigneeResponsibility = false)
    {
        try
        {
            return await ExecuteCorrectiveCoreAsync(id, request, incidentNote, operation, cancellationToken,
                prepareOwners, restoreAssigneeResponsibility);
        }
        catch (WorkTimeContractException error)
        {
            // The inner transaction has already been disposed without commit. Reuse the
            // existing HTTP boundary's sanitized status handling; expose no source IDs.
            throw new Dev2AccessDenied(error.Code switch
            {
                "TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID" => 403,
                "TIME_STALE" => 412,
                "TIME_SOURCE_UNRESOLVED" or "TIME_BUSY" => 503,
                _ => 409
            });
        }
    }

    // This is a transaction participant behind RequireEntry, not new HTTP authority.
    private static bool OpensWorkInterval(MaintenanceActorRequest request)
        => request is StartMaintenanceWorkRequest or ResumeMaintenanceWorkRequest;

    private static bool ClosesWorkInterval(MaintenanceActorRequest request)
        => request is HoldMaintenanceWorkRequest or SubmitMaintenanceResultRequest or CancelMaintenanceWorkRequest;

    private async Task<DateTime?> CloseWorkIntervalAsync(SqlWorkTimeTransactionParticipant time, MaintenanceWork work,
        Incident incident, CancellationToken ct)
    {
        if (work.Status != "IN_PROGRESS")
        {
            if (await _dbContext.WorkIntervals.AsNoTracking().AnyAsync(i => i.WorkItemKind == "MAINTENANCE"
                    && i.WorkItemId == work.MaintenanceWorkId && i.ClosedAt == null, ct))
                throw new WorkTimeContractException("TIME_SOURCE_UNRESOLVED");
            return null;
        }
        if (work.AssigneeId is not long owner)
            throw new WorkTimeContractException("TIME_SOURCE_UNRESOLVED");
        var open = await _dbContext.WorkIntervals.AsNoTracking().Where(i => i.UserId == owner && i.ClosedAt == null)
            .Take(2).ToArrayAsync(ct);
        if (open.Length != 1 || open[0].WorkItemKind != "MAINTENANCE" || open[0].WorkItemId != work.MaintenanceWorkId
            || open[0].BranchId != incident.BranchId)
            throw new WorkTimeContractException("TIME_SOURCE_UNRESOLVED");
        // Do not manufacture legacy effort when an existing running work has no interval.
        await Dev2ServiceAuthority.RevalidateAsync(_dbContext, _authorityHttp, Dev2AuthorizationPhase.BeforePersistence, ct);
        var closed = await time.CloseIntervalAsync(new(TrustedActorId, owner, incident.BranchId, "MAINTENANCE", work.MaintenanceWorkId),
            open[0].IntervalId, open[0].RowVersion, ct);
        return closed.ClosedAtUtc;
    }

    private async Task<DateTime> OpenWorkIntervalAsync(SqlWorkTimeTransactionParticipant time, MaintenanceWork work,
        Incident incident, CancellationToken ct)
    {
        RequireAssignee(work, TrustedActorId);
        // Persist the owning transition first, still in the caller transaction, so the shared
        // participant can re-read the canonical source. Any interval failure rolls it all back.
        await Dev2ServiceAuthority.SaveChangesAsync(_dbContext, _authorityHttp, ct);
        var opened = await time.OpenIntervalAsync(new(TrustedActorId, TrustedActorId, incident.BranchId,
            "MAINTENANCE", work.MaintenanceWorkId), ct);
        return opened.OpenedAtUtc;
    }
}
