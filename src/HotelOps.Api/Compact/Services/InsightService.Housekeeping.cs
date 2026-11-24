using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    public async Task<object> Housekeeping(long branchId, CancellationToken ct)
    {
        await access.Require("DASHBOARD_VIEW", branchId, ct: ct);
        var roomView = await access.Allows("ROOM_VIEW", branchId, ct: ct);
        var cleaningView = await access.Allows("CLEANING_VIEW", branchId, ct: ct);
        var inspectionView = await access.Allows("INSPECTION_VIEW", branchId, ct: ct);
        var incidentView = await access.Allows("INCIDENT_VIEW", branchId, ct: ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var rooms = from r in db.Rooms.AsNoTracking()
            join v in db.VRoomReadinesses on r.RoomId equals v.RoomId
            where r.BranchId == branchId && r.IsActive select v;
        var cleaning = db.WorkItems.AsNoTracking().Where(w => w.BranchId == branchId && w.Kind == "CLEANING" && !WorkRules.Terminal.Contains(w.Status));
        var inspections = db.WorkItems.AsNoTracking().Where(w => w.BranchId == branchId && w.Kind == "INSPECTION" && !WorkRules.Terminal.Contains(w.Status));
        var rework = db.WorkItems.AsNoTracking().Where(w => w.BranchId == branchId && w.Kind == "REWORK" && !WorkRules.Terminal.Contains(w.Status));
        var incidents = db.WorkItems.AsNoTracking().Where(w => w.BranchId == branchId && w.Kind == "INCIDENT" && w.RoomId != null && w.Status != "CLOSED" && w.Status != "RESOLVED" && w.Status != "CANCELLED");
        return new { branchId, updatedAt = now, metrics = new {
            roomsNeedCleaning = roomView ? (int?)await rooms.CountAsync(r => r.Readiness == "PENDING_CLEANING", ct) : null,
            roomsInProgress = roomView ? (int?)await rooms.CountAsync(r => r.Readiness == "CLEANING_IN_PROGRESS", ct) : null,
            roomsPendingInspection = cleaningView ? (int?)await cleaning.CountAsync(w => w.Status == "PENDING_INSPECTION" || (w.Status == "REWORK_REQUIRED" && db.WorkItems.Any(r => r.ParentWorkId == w.WorkId && r.Kind == "REWORK" && r.Status == "PENDING_REINSPECTION" && !db.WorkItems.Any(i=>i.ParentWorkId==w.WorkId&&i.Kind=="INSPECTION"&&i.Status=="FAILED"&&i.WorkId>r.WorkId&&i.VerifiedAt>=r.CompletedAt))), ct) : null,
            roomsReady = roomView ? (int?)await rooms.CountAsync(r => r.Readiness == "READY", ct) : null,
            unassigned = cleaningView ? (int?)await cleaning.CountAsync(w => !db.WorkAssignments.Any(a => a.WorkId == w.WorkId && a.AssignmentStatus == "ACTIVE"), ct) : null,
            inProgress = cleaningView ? (int?)await cleaning.CountAsync(w => w.Status == "IN_PROGRESS", ct) : null,
            overdue = cleaningView ? (int?)await cleaning.CountAsync(w => w.DueAt < now, ct) : null,
            pendingInspection = inspectionView ? (int?)await inspections.CountAsync(ct) : null,
            rework = cleaningView ? (int?)await rework.CountAsync(ct) : null,
            incidents = incidentView ? (int?)await incidents.CountAsync(ct) : null,
            defects = inspectionView ? (int?)await db.InspectionFindings.AsNoTracking().CountAsync(f => f.BranchId == branchId && f.Status == "OPEN", ct) : null
        },
        turnover = roomView ? await rooms.GroupBy(r => new { r.Readiness, r.IsOccupied }).Select(g => new { status = g.Key.Readiness, occupied = g.Key.IsOccupied, count = g.Count() }).ToArrayAsync(ct) : null,
        workload = cleaningView ? await cleaning.GroupBy(w => w.Status).Select(g => new { status = g.Key, count = g.Count() }).ToArrayAsync(ct) : null,
        priority = cleaningView ? await (from w in cleaning join r in db.Rooms on w.RoomId equals r.RoomId
            orderby w.DueAt == null, w.DueAt, w.WorkId select new { w.WorkId, w.Kind, w.Title, w.Status, w.DueAt, room = r.Code }).Take(10).ToArrayAsync(ct) : null,
        waiting = cleaningView ? await (from w in cleaning join r in db.Rooms on w.RoomId equals r.RoomId
            where w.Status == "PENDING_INSPECTION" || (w.Status == "REWORK_REQUIRED" && db.WorkItems.Any(x => x.ParentWorkId == w.WorkId && x.Kind == "REWORK" && x.Status == "PENDING_REINSPECTION" && !db.WorkItems.Any(i=>i.ParentWorkId==w.WorkId&&i.Kind=="INSPECTION"&&i.Status=="FAILED"&&i.WorkId>x.WorkId&&i.VerifiedAt>=x.CompletedAt)))
            orderby w.CompletedAt, w.WorkId select new { w.WorkId, w.Kind, w.Title, w.Status, room = r.Code }).Take(10).ToArrayAsync(ct) : null,
        overdue = cleaningView ? await cleaning.Where(w => w.DueAt < now).OrderBy(w => w.DueAt).Take(10).Select(w => new { w.WorkId, w.Kind, w.Title, w.Status, w.DueAt }).ToArrayAsync(ct) : null
        };
    }
}
