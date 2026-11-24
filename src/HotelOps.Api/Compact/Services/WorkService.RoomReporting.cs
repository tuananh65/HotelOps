using Microsoft.EntityFrameworkCore;
using HotelOps.Api.Compact.Entities;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private async Task<bool> CanReportDuringHousekeeping(WorkItem work, CancellationToken ct)
    {
        if (!await access.Allows("IR_CREATE", work.BranchId, true, ct: ct) || work.RoomId == null
            || !await db.Rooms.AnyAsync(r => r.RoomId == work.RoomId && r.BranchId == work.BranchId && r.IsActive && r.TurnoverNo == work.TurnoverNo, ct)) return false;
        if (work.Kind is "CLEANING" or "REWORK" && work.Status == "IN_PROGRESS"
            && await access.HasRole("HK_STAFF", work.BranchId, ct))
            return await db.WorkAssignments.AnyAsync(a => a.WorkId == work.WorkId && a.AssigneeUserId == actor.Id && a.AssignmentStatus == "ACTIVE" && a.AcceptedAt != null, ct);
        if (!await access.HasRole("HOUSEKEEPING_SUPERVISOR", work.BranchId, ct)
            || !await access.Allows("INSPECTION_PERFORM", work.BranchId, ct: ct)) return false;
        if (work.Kind == "INSPECTION") return work.Status == "INSPECTING" && await db.WorkAssignments.AnyAsync(a => a.WorkId == work.WorkId && a.AssigneeUserId == actor.Id && a.AssignmentStatus == "ACTIVE", ct);
        if (work.Kind != "CLEANING" || work.Status is not ("PENDING_INSPECTION" or "REWORK_REQUIRED")) return false;
        if (await db.WorkAssignments.AnyAsync(a => a.AssigneeUserId == actor.Id && (a.WorkId == work.WorkId || db.WorkItems.Any(r => r.WorkId == a.WorkId && r.ParentWorkId == work.WorkId && r.Kind == "REWORK")), ct)) return false;
        if (await db.WorkItems.AnyAsync(r => r.ParentWorkId == work.WorkId && r.Kind == "REWORK" && !WorkRules.Terminal.Contains(r.Status) && r.Status != "PENDING_REINSPECTION", ct)) return false;
        return work.Status == "PENDING_INSPECTION" || await db.WorkItems.AnyAsync(r => r.ParentWorkId == work.WorkId && r.Kind == "REWORK" && r.Status == "PENDING_REINSPECTION"
            && !db.WorkItems.Any(i => i.ParentWorkId == work.WorkId && i.Kind == "INSPECTION" && i.Status == "FAILED" && i.WorkId > r.WorkId && i.VerifiedAt >= r.CompletedAt), ct);
    }
}
