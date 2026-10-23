using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    private static MaintenanceCycleSnapshot Snapshot(MaintenanceWork w, Incident i) => new(w.AssigneeId,
        w.DepartmentId, w.PriorityId, w.Title, w.Instruction, w.PlannedStartAt, w.PlannedEndAt,
        w.ActualStartAt, w.ResolvedAt, w.ResolutionNote, w.ReviewedBy, w.ReviewedAt, i.ResolvedAt, i.ClosedAt, i.ClosureNote);
    private static MaintenanceCycleSnapshot Snapshot(MaintenanceWorkCycle c) => new(c.AssigneeId,
        c.DepartmentId, c.PriorityId, c.Title, c.Instruction, c.PlannedStartAt, c.PlannedEndAt,
        c.ActualStartAt, c.ResolvedAt, c.ResolutionNote, c.ReviewedBy, c.ReviewedAt, c.IncidentResolvedAt, c.IncidentClosedAt, c.IncidentClosureNote);
    private static void Capture(MaintenanceWorkCycle c, MaintenanceWork w, Incident i)
    {
        c.AssigneeId = w.AssigneeId; c.DepartmentId = w.DepartmentId; c.PriorityId = w.PriorityId;
        c.Title = w.Title; c.Instruction = w.Instruction; c.PlannedStartAt = w.PlannedStartAt; c.PlannedEndAt = w.PlannedEndAt;
        c.ActualStartAt = w.ActualStartAt; c.ResolvedAt = w.ResolvedAt; c.ResolutionNote = w.ResolutionNote;
        c.ReviewedBy = w.ReviewedBy; c.ReviewedAt = w.ReviewedAt;
        c.IncidentResolvedAt = i.ResolvedAt; c.IncidentClosedAt = i.ClosedAt; c.IncidentClosureNote = i.ClosureNote;
    }
    // Caller owns Incident -> Work locks. Never acquires Catalog or infers history by timestamps.
    private async Task<MaintenanceWorkCycle?> CycleContext(MaintenanceWork work, Incident incident, CancellationToken ct)
    {
        var cycles = await _dbContext.MaintenanceWorkCycles.Where(x => x.MaintenanceWorkId == work.MaintenanceWorkId).OrderBy(x => x.CycleNo).ToListAsync(ct);
        if (cycles.Count == 0) return null;
        if (cycles.Count < 2 || !cycles[0].IsLegacySnapshot || cycles[0].CycleNo != 1 || cycles[0].FinalizedAt is null ||
            cycles.Where((c, n) => c.CycleNo != n + 1 || n > 0 && c.IsLegacySnapshot || n < cycles.Count - 1 && c.FinalizedAt is null).Any())
            throw new ValidationException("Cycle history is inconsistent; no automatic repair is performed.");
        var last = cycles[^1];
        if (work.Status is "CLOSED" or "CANCELLED")
        {
            if (last.FinalizedAt is null || last.TerminalStatus != work.Status || Snapshot(last) != Snapshot(work, incident))
                throw new ValidationException("Terminal cycle/header snapshot mismatch.");
        }
        else if (last.FinalizedAt is not null || last.IsLegacySnapshot)
            throw new ValidationException("Current Work requires an active cycle.");
        return last;
    }
    private static void ExpectedCycle(MaintenanceWorkCycle? cycle, long? expected, bool requireActive)
    {
        if (expected != cycle?.CycleId || requireActive && cycle?.FinalizedAt is not null)
            throw new ValidationException("ExpectedCycleId must match the current active cycle. Reload before retrying.");
    }
    private Dictionary<string, object?> CycleAudit(MaintenanceWorkCycle cycle) => _dbContext.Entry(cycle).Properties
        .ToDictionary(x => x.Metadata.GetColumnName(), x => x.CurrentValue);
    private void FinalizeCycle(MaintenanceWorkCycle cycle, MaintenanceWork work, Incident incident, long actor, string reason, DateTime now)
    {
        if (cycle.FinalizedAt is not null) throw new ValidationException("Cycle is already finalized.");
        var before = CycleAudit(cycle);
        Capture(cycle, work, incident); cycle.FinalizedAt = now; cycle.EndedAt = now; cycle.EndedBy = actor;
        cycle.TerminalStatus = work.Status; cycle.TerminalReason = reason;
        AddAudit("maintenance_work_cycles", cycle.CycleId, incident.BranchId, actor, now, before, CycleAudit(cycle));
    }
    public async Task<MaintenanceWorkDetailDto?> ReopenWorkAsync(long id, ReopenMaintenanceWorkRequest request, CancellationToken cancellationToken)
    {
        var reason = RequiredNote(request.Reason, "Reason", 10);
        var discovered = await _dbContext.MaintenanceWorks.AsNoTracking().Where(x => x.MaintenanceWorkId == id).Select(x => new { x.IncidentId }).SingleOrDefaultAsync(cancellationToken);
        if (discovered is null) return null;
        if (discovered.IncidentId is not long incidentId) throw new ValidationException("Corrective Work required.");
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var incident = await LockIncidentAsync(incidentId, cancellationToken);
        var work = await _dbContext.MaintenanceWorks.FromSqlInterpolated($"SELECT * FROM maintenance_works WITH (UPDLOCK, HOLDLOCK) WHERE maintenance_work_id = {id}").SingleOrDefaultAsync(cancellationToken);
        if (work is null) return null;
        if (incident is null || work.IncidentId != incidentId) throw new ValidationException("Corrective linkage changed.");
        RequireSynchronizedPair(work, incident); RequireState(work, "CLOSED");
        await RequireUserAsync(request.ActorUserId, "ActorUserId", cancellationToken);
        if (await _dbContext.PreventiveMaintenanceRuns.AnyAsync(x => x.MaintenanceWorkId == id, cancellationToken) ||
            await _dbContext.MaintenanceWorks.AnyAsync(x => x.IncidentId == incidentId && x.MaintenanceWorkId != id, cancellationToken))
            throw new ValidationException("Ambiguous or preventive Work cannot be reopened.");
        if (incident.ClosedAt is not DateTime closed) throw new ValidationException("Reliable Incident ClosedAt is required.");
        var now = _clock.GetUtcNow().UtcDateTime;
        DateTime deadline;
        try { deadline = DateTime.SpecifyKind(closed, DateTimeKind.Utc).AddDays(_options.ReopenWindowDays); }
        catch (ArgumentOutOfRangeException) { throw new ValidationException("Reopen window exceeds the supported date range."); }
        if (now > deadline) throw new ValidationException("The configured maintenance reopen window has expired.");
        var previous = await CycleContext(work, incident, cancellationToken);
        ExpectedCycle(previous, request.ExpectedCycleId, false);
        var beforeWork = WorkSnapshot(work); beforeWork["current_cycle_id"] = previous?.CycleId;
        var beforeIncident = IncidentSnapshot(incident);
        if (previous is null)
        {
            var legacy = new MaintenanceWorkCycle { MaintenanceWorkId = id, CycleNo = 1, IsLegacySnapshot = true,
                RecordedAt = now, RecordedBy = request.ActorUserId, FinalizedAt = now, TerminalStatus = "CLOSED" };
            Capture(legacy, work, incident);
            // Unknown historical actors/times remain unknown; capture time is not closure time.
            _dbContext.MaintenanceWorkCycles.Add(legacy); await _dbContext.SaveChangesAsync(cancellationToken);
            var legacyAudit = CycleAudit(legacy); legacyAudit["business_event"] = "LEGACY_CYCLE_CAPTURED";
            AddAudit("maintenance_work_cycles", legacy.CycleId, incident.BranchId, request.ActorUserId, now, null, legacyAudit);
            previous = legacy;
        }
        if (previous.CycleNo == int.MaxValue) throw new ValidationException("Cycle number exhausted.");
        var next = new MaintenanceWorkCycle { MaintenanceWorkId = id, CycleNo = previous.CycleNo + 1,
            RecordedAt = now, RecordedBy = request.ActorUserId, OpenedAt = now, OpenedBy = request.ActorUserId,
            ReopenReason = reason, StartingWorkStatus = "PENDING", StartingIncidentStatus = "OPEN" };
        _dbContext.MaintenanceWorkCycles.Add(next); await _dbContext.SaveChangesAsync(cancellationToken);
        var openedAudit = CycleAudit(next); openedAudit["business_event"] = "MAINTENANCE_CYCLE_OPENED";
        openedAudit["previous_cycle_id"] = previous.CycleId; openedAudit["new_cycle_id"] = next.CycleId;
        AddAudit("maintenance_work_cycles", next.CycleId, incident.BranchId, request.ActorUserId, now, null, openedAudit);
        work.Status = "PENDING"; work.AssigneeId = null; work.PlannedStartAt = null; work.PlannedEndAt = null;
        work.ActualStartAt = null; work.ResolvedAt = null; work.ResolutionNote = null; work.ReviewedBy = null; work.ReviewedAt = null;
        incident.Status = "OPEN"; incident.ResolvedAt = null; incident.ClosedAt = null; incident.ClosureNote = null;
        var afterWork = WorkSnapshot(work); afterWork["current_cycle_id"] = next.CycleId;
        AddAudit("maintenance_works", id, incident.BranchId, request.ActorUserId, now, beforeWork, afterWork);
        AddAudit("incidents", incidentId, incident.BranchId, request.ActorUserId, now, beforeIncident, IncidentSnapshot(incident));
        _dbContext.IncidentEvents.Add(new() { IncidentId = incidentId, EventType = "STATUS_CHANGED", FromStatus = "CLOSED", ToStatus = "OPEN", ActorId = request.ActorUserId, OccurredAt = now, Note = reason });
        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await RequireDetailAsync(id, cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }
    // Consistent multi-query reads reuse an existing caller transaction; never take Catalog late.
    private async Task<bool> LockCycleRead(long id, CancellationToken ct)
    {
        var owner = await _dbContext.MaintenanceWorks.AsNoTracking().Where(x => x.MaintenanceWorkId == id).Select(x => new { x.IncidentId }).SingleOrDefaultAsync(ct);
        if (owner is null) return false;
        if (owner.IncidentId is long incident) await LockIncidentAsync(incident, ct);
        return await _dbContext.MaintenanceWorks.FromSqlInterpolated($"SELECT * FROM maintenance_works WITH (UPDLOCK, HOLDLOCK) WHERE maintenance_work_id = {id}").AsNoTracking().AnyAsync(ct);
    }
    public async Task<MaintenanceCyclePage?> GetCyclesAsync(long id, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) throw new ValidationException("Invalid cycle paging.");
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCycleRead(id, cancellationToken)) return null;
        var query = _dbContext.MaintenanceWorkCycles.AsNoTracking().Where(x => x.MaintenanceWorkId == id);
        var count = await query.CountAsync(cancellationToken);
        var cycles = await query.OrderBy(x => x.CycleNo).ThenBy(x => x.CycleId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = new List<MaintenanceCycleDto>();
        foreach (var c in cycles)
        {
            var logs = _dbContext.MaintenanceWorkLogs.AsNoTracking().Where(x => x.MaintenanceWorkId == id && (x.CycleId == c.CycleId || c.IsLegacySnapshot && x.CycleId == null));
            var logCount = await logs.CountAsync(cancellationToken);
            var usageCount = await _dbContext.MaintenanceMaterialUsages.CountAsync(x => logs.Any(l => l.LogId == x.MaintenanceWorkLogId), cancellationToken);
            items.Add(new(c.CycleId, id, c.CycleNo, c.IsLegacySnapshot, c.RecordedAt, c.RecordedBy, c.OpenedAt, c.OpenedBy, c.ReopenReason,
                c.StartingWorkStatus, c.StartingIncidentStatus, c.FinalizedAt, c.TerminalStatus, c.EndedAt, c.EndedBy, c.TerminalReason, Snapshot(c), logCount, usageCount));
        }
        await tx.CommitAsync(cancellationToken); return new(page, pageSize, count, items);
    }
}
