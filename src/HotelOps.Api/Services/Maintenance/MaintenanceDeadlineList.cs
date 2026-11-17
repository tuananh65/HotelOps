using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceDeadlineList
{
    internal sealed record Snapshot(MaintenancePagedResponse Page, IncidentReadAccess.Scope IncidentScope,
        DateTime EvaluatedAt, IReadOnlyDictionary<long, MaintenanceDeadlineView> Deadlines);

    internal static async Task<Snapshot> ReadAsync(HotelOpsDbContext db, long user, long branch,
        IQueryable<MaintenanceWork> scoped, MaintenanceQueryRequest input, CancellationToken ct,
        Func<DateTime, CancellationToken, Task<IReadOnlyList<MaintenanceWorkListDto>>>? occurrences = null)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Deadline list requires an owning read transaction.");
        if (input.DueFrom != null && input.DueTo != null && input.DueTo < input.DueFrom)
            throw new ValidationException("Due range end must not precede its start.");
        var filtered = MaintenanceWorkService.Filter(scoped, input);
        var incidentScope = await IncidentReadAccess.ReadAsync(db, user, branch, ct, sourceRecord: true);
        var now = DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value")
            .SingleAsync(ct), DateTimeKind.Utc);
        var matches = new List<(long Id, MaintenanceDeadlineView Deadline)>();
        long after = 0;
        // Resolve deadlines before counting/paging. Closed Incident work needs its historical
        // cycle receipt, not the current Incident's (possibly reopened) SLA. Reuse that reader.
        while (true)
        {
            var ids = await filtered.Where(w => w.MaintenanceWorkId > after).OrderBy(w => w.MaintenanceWorkId)
                .Select(w => w.MaintenanceWorkId).Take(100).ToArrayAsync(ct);
            if (ids.Length == 0) break;
            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                var deadline = await MaintenanceDeadlineRead.ReadAsync(db, user, branch, id, now, ct);
                if (input.DueFrom is {} from && (deadline.DueAt == null || deadline.DueAt < from.UtcDateTime)
                    || input.DueTo is {} to && (deadline.DueAt == null || deadline.DueAt > to.UtcDateTime)
                    || input.Overdue is {} overdue && deadline.IsOverdue != overdue) continue;
                matches.Add((id, deadline));
            }
            after = ids[^1];
        }
        // Null deadlines sort last. Unknown overdue is not the same as false.
        var occurrenceRows = occurrences == null ? [] : await occurrences(now, ct);
        var occurrenceMap = occurrenceRows.ToDictionary(r => r.OccurrenceId!.Value);
        var combined = matches.Select(m => (m.Id, Occurrence: false, m.Deadline))
            .Concat(occurrenceRows.Select(r => (Id: r.OccurrenceId!.Value, Occurrence: true, Deadline: r.Deadline!)));
        var total = checked(matches.Count + occurrenceRows.Count);
        var selected = combined.OrderBy(m => m.Deadline.DueAt == null).ThenBy(m => m.Deadline.DueAt)
            .ThenBy(m => m.Id).ThenBy(m => m.Occurrence)
            .Skip(checked((input.Page - 1) * input.PageSize)).Take(input.PageSize).ToArray();
        var selectedIds = selected.Where(m => !m.Occurrence).Select(m => m.Id).ToArray();
        var page = await MaintenanceWorkService.ListAsync(scoped.Where(w => selectedIds.Contains(w.MaintenanceWorkId)),
            new MaintenanceQueryRequest { BranchId = branch, Page = 1, PageSize = input.PageSize }, ct);
        var rows = page.Items.ToDictionary(w => w.MaintenanceWorkId!.Value);
        if (rows.Count != selectedIds.Length) throw new InvalidOperationException("Maintenance list scope changed.");
        var ordered = selected.Select(m =>
        {
            var row = m.Occurrence ? occurrenceMap[m.Id] : rows[m.Id];
            row.Deadline = m.Deadline;
            return row;
        }).ToArray();
        return new(new MaintenancePagedResponse
        {
            Items = ordered, Page = input.Page, PageSize = input.PageSize, TotalCount = total,
            TotalPages = (int)(((long)total + input.PageSize - 1) / input.PageSize)
        }, incidentScope, now, selected.Where(m => !m.Occurrence).ToDictionary(m => m.Id, m => m.Deadline));
    }

    internal static async Task<bool> CanDiscloseAsync(HotelOpsDbContext db, long user, long branch,
        Snapshot snapshot, CancellationToken ct)
    {
        // Protect totals, ordering and empty pages as well as the displayed rows.
        if (await IncidentReadAccess.ReadAsync(db, user, branch, ct, sourceRecord: true) != snapshot.IncidentScope)
            return false;
        foreach (var (id, shown) in snapshot.Deadlines)
            if (await MaintenanceDeadlineRead.ReadAsync(db, user, branch, id, snapshot.EvaluatedAt, ct) != shown)
                return false;
        return true;
    }
}
