using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// Cycle identity is evidence, not a replacement for the approved allowed-start timestamp.
internal static class WorkAlertCycle
{
    internal static async Task<long> ReadAsync(HotelOpsDbContext db, string kind, long id, DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Cycle evidence requires a serializable source observation.");
        if (kind == "TASK")
        {
            if (!await TaskReopenStore.InstalledAsync(db, ct))
                throw new InvalidOperationException("Task cycle evidence unavailable.");
            var count = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_reopen_events WHERE task_id={id}").SingleAsync(ct);
            var proven = await TaskReopenStore.Query(db).Where(r => r.TaskId == id).ToArrayAsync(ct);
            if (proven.Length != count || proven.Any(r => r.ReopenedAt > now))
                throw new InvalidOperationException("Task cycle provenance inconsistent.");
            return proven.OrderByDescending(r => r.ReopenedAt).ThenByDescending(r => r.AuditId).Select(r => r.AuditId).FirstOrDefault();
        }
        if (kind == "INTERNAL_REQUEST")
        {
            if (!await InternalRequestReopenService.InstalledAsync(db, ct))
                throw new InvalidOperationException("Internal request cycle evidence unavailable.");
            var count = await db.InternalRequestEvents.AsNoTracking().CountAsync(e => e.InternalRequestId == id && e.EventType == "REOPEN", ct);
            var proven = await InternalRequestReopenDelivery.Query(db).Where(r => r.RequestId == id).ToArrayAsync(ct);
            if (proven.Length != count || proven.Any(r => r.At > now))
                throw new InvalidOperationException("Internal request cycle provenance inconsistent.");
            return proven.OrderByDescending(r => r.At).ThenByDescending(r => r.EventId).Select(r => r.EventId).FirstOrDefault();
        }
        throw new ArgumentException("Unsupported cycle source.", nameof(kind));
    }
}
