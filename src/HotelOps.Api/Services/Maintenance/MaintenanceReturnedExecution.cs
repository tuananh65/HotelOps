using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

// Current Return proof permits the executor to restart effort without inventing a workflow state.
// Caller owns serializable transaction, current execution authority and (on write) user/time/source locks.
internal static class MaintenanceReturnedExecution
{
    internal static async Task<MaintenanceReturnSource.Source?> ReadAsync(HotelOpsDbContext db, long work,
        long actor, long branch, CancellationToken ct)
    {
        if (!await db.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.MaintenanceWorkId == work
            && w.AssigneeId == actor && w.Status == "IN_PROGRESS" && w.ActualStartAt != null, ct)) return null;
        var entity = work.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var candidates = await db.Database.SqlQuery<long>($"""
            SELECT TOP(1) audit_id AS Value FROM dbo.audit_logs WITH(HOLDLOCK)
            WHERE entity_type='maintenance_works' AND entity_id={entity} AND action='UPDATE'
              AND JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.notification_kind')='MAINTENANCE_RETURNED'
            ORDER BY audit_id DESC
            """).ToArrayAsync(ct);
        if (candidates.Length != 1) return null;
        var source = await MaintenanceReturnSource.ReadAsync(db, candidates[0], ct);
        if (source == null || source.WorkId != work || source.BranchId != branch || source.RecipientId != actor) return null;
        // Any interval opened for this source since Return consumes the restart opportunity,
        // even if later closed. No reconstruction of missing legacy execution history.
        if (await db.WorkIntervals.AsNoTracking().AnyAsync(i => i.WorkItemKind == "MAINTENANCE" && i.WorkItemId == work
            && (i.ClosedAt == null || i.OpenedAt >= source.At || i.ClosedAt > source.At), ct)) return null;
        return source;
    }
}
