using HotelOps.Api.Data;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal static class TaskSeriesResponsibilities
{
    internal static async Task<UserResponsibilityAssessment> ReadAsync(HotelOpsDbContext db, long target, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Task series review requires the account lifecycle transaction.");
        if (!await TaskSeriesStore.InstalledAsync(db, ct))
            return new([], ["TASK_SERIES_RESPONSIBILITY_STORAGE_REQUIRED"]);
        var rows = await TaskSeriesStore.Query(db).Where(s => s.CreatedBy == target || s.DefaultAssigneeId == target)
            .ToArrayAsync(ct);
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        foreach (var row in rows)
        {
            // JOB05 locks and revalidates both identities before every generation batch.
            // A finite series with an exhausted cursor has no future assignment obligation.
            if (row.IsActive && row.NextDueAt is not null)
            {
                if (row.CreatedBy == target) blockers.Add($"TASK_SERIES_INITIATOR:{row.SeriesId}");
                if (row.DefaultAssigneeId == target) blockers.Add($"TASK_SERIES_EXECUTOR:{row.SeriesId}");
            }
            if (row.CreatedAt > now || row.NextDueAt < row.FirstDueAt
                || row.IsActive && (row.StoppedAt != null || row.StoppedBy != null)
                || !row.IsActive && (row.StoppedAt == null || row.StoppedBy == null)
                || row.StoppedAt < row.CreatedAt || row.StoppedAt > now
                || row.IsActive && row.NextDueAt == null && row.EndDate == null)
                unresolved.Add($"TASK_SERIES_STATE_INCONSISTENT:{row.SeriesId}");
        }
        return new(blockers.ToArray(), unresolved.ToArray());
    }
}
