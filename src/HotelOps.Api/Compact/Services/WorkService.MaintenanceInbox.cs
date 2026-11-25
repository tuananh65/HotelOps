using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    public async Task<object> MaintenanceInbox(long branchId, string queue, int page, string? search, CancellationToken ct)
    {
        await access.Require("MAINTENANCE_VIEW", branchId, ct: ct);
        await access.Require("INCIDENT_VIEW", branchId, ct: ct);
        Output.Require(await access.HasRole("MAINTENANCE_SUPERVISOR", branchId, ct), "MAINTENANCE_SUPERVISOR_REQUIRED", 403);
        var issues = db.WorkEvents.Where(e => e.EventType == "ROOM_ISSUE_REPORTED" || e.EventType == "LEGACY_ROOM_ISSUE_RECOVERED").Select(e => e.WorkId);
        // A room issue owns its repair attempts. Show that root once, including rework.
        var work = db.WorkItems.AsNoTracking().Where(w => w.BranchId == branchId &&
            ((w.Kind == "INCIDENT" && issues.Contains(w.WorkId) && db.WorkEvents.Any(e => e.WorkId == w.WorkId && e.EventType == "ROOM_ISSUE_REPAIR_REQUESTED")) ||
             (w.Kind == "MAINTENANCE" && (w.ParentWorkId == null || !issues.Contains(w.ParentWorkId.Value)))));
        var rows = work.Select(w => new {
            w.WorkId, w.Kind, w.Title, w.Status, w.RoomId,
            room = db.Rooms.Where(r => r.RoomId == w.RoomId).Select(r => r.Code).FirstOrDefault(),
            stage = db.WorkEvents.Where(e => e.WorkId == w.WorkId && e.EventType.StartsWith("ROOM_ISSUE_"))
                .OrderByDescending(e => e.WorkEventId).Select(e => e.EventType).FirstOrDefault(),
            updatedAt = db.WorkEvents.Where(e => e.WorkId == w.WorkId).Max(e => (DateTime?)e.OccurredAt) ?? w.CreatedAt
        }).Select(w => new {
            w.WorkId, w.Kind, w.Title, w.Status, w.RoomId, w.room, w.stage, w.updatedAt,
            queue = w.Status == "CLOSED" || w.Status == "CANCELLED" || w.Status == "REJECTED" || w.stage == "ROOM_ISSUE_INSPECTION_PASSED" ? "done" :
                w.stage == "ROOM_ISSUE_REMEDY_REPORTED" || w.stage == "ROOM_ISSUE_INSPECTION_REQUESTED" || (w.Kind == "MAINTENANCE" && w.Status == "COMPLETED") ? "inspection" :
                w.stage == "ROOM_ISSUE_REPAIR_CREATED" || (w.Kind == "MAINTENANCE" && (w.Status == "IN_PROGRESS" || w.Status == "ON_HOLD")) ? "doing" : "waiting"
        });
        Output.Require(new[] { "all", "waiting", "doing", "inspection", "done" }.Contains(queue), "INVALID_QUEUE", 400);
        var counts = await rows.GroupBy(w => w.queue).Select(g => new { queue = g.Key, count = g.Count() }).ToArrayAsync(ct);
        rows = queue == "all" ? rows.Where(w => w.queue != "done") : rows.Where(w => w.queue == queue);
        if (!string.IsNullOrWhiteSpace(search)) {
            var term = search.Trim();
            rows = rows.Where(w => w.Title.Contains(term) || (w.room != null && w.room.Contains(term)));
        }
        var total = await rows.CountAsync(ct);
        page = Math.Clamp(page, 1, 1000000);
        var items = await rows.OrderByDescending(w => w.updatedAt).ThenByDescending(w => w.WorkId).Skip((page - 1) * 50).Take(50).ToArrayAsync(ct);
        return new { items, counts, total, page, pageSize = 50 };
    }
}
