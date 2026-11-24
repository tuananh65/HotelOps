using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    // The same physical PASS covers the completed cleaning cycle. Reuse its
    // inspection guards/evidence instead of requiring another user confirmation.
    private async Task CompleteCleaningAfterRemediation(WorkItem issue, long? sourceId,
        RoomIssueActionRequest request, CancellationToken ct)
    {
        if (sourceId == null) return;
        var source = await db.WorkItems.SingleOrDefaultAsync(w => w.WorkId == sourceId
            && w.BranchId == issue.BranchId && w.RoomId == issue.RoomId, ct);
        if (source == null) return;
        var rootId = source.Kind == "CLEANING" ? source.WorkId : source.ParentWorkId;
        if (rootId == null) return;
        var root = await db.WorkItems.SingleAsync(w => w.WorkId == rootId, ct);
        if (root.Kind != "CLEANING" || root.Status is not ("PENDING_INSPECTION" or "REWORK_REQUIRED")) return;
        if (!await db.Rooms.AnyAsync(r => r.RoomId == root.RoomId && r.IsActive
            && r.CurrentCleaningWorkId == root.WorkId && r.TurnoverNo == root.TurnoverNo, ct)) return;
        if (await CleaningIssues(root.WorkId).AnyAsync(i => i.Status != "CLOSED" && i.Status != "CANCELLED", ct)) return;
        if (!await CanReportDuringHousekeeping(root, ct)) return;
        var inspection = await db.WorkItems.Where(w => w.ParentWorkId == root.WorkId && w.Kind == "INSPECTION"
            && !WorkRules.Terminal.Contains(w.Status)).SingleOrDefaultAsync(ct);
        await PaperInspection(root.WorkId, new PaperInspectionRequest(Convert.ToBase64String(root.RowVersion),
            request.OperationKey, "PASS", request.Note, InspectionRowVersion: inspection == null ? null : Convert.ToBase64String(inspection.RowVersion)), ct);
    }

    private IQueryable<long> CleaningAfterRework(long branch) => db.WorkItems.Where(w => w.BranchId == branch
        && w.Kind == "CLEANING" && w.Status == "REWORK_REQUIRED"
        && db.WorkItems.Any(r => r.ParentWorkId == w.WorkId && r.Kind == "REWORK" && r.Status == "PENDING_REINSPECTION"
            && r.CompletedAt != null && !db.WorkItems.Any(i => i.ParentWorkId == w.WorkId && i.Kind == "INSPECTION"
                && i.Status == "FAILED" && i.VerifiedAt >= r.CompletedAt))
        && !db.WorkItems.Any(r => r.ParentWorkId == w.WorkId && r.Kind == "REWORK"
            && !WorkRules.Terminal.Contains(r.Status) && r.Status != "PENDING_REINSPECTION")).Select(w => w.WorkId);

    private async Task<long[]> CleaningAfterRemediation(long branch, CancellationToken ct)
    {
        var issues = await (from link in db.WorkLinks.AsNoTracking()
            join issue in db.WorkItems on link.SourceWorkId equals issue.WorkId
            join target in db.WorkItems on link.TargetWorkId equals target.WorkId
            where link.BranchId == branch && link.Relation == "SOURCE" && issue.Kind == "INCIDENT"
                && issue.Status != "CLOSED" && issue.Status != "CANCELLED"
                && (target.Kind == "CLEANING" || target.Kind == "REWORK")
            select new { issue.WorkId, Root = target.Kind == "REWORK" ? target.ParentWorkId!.Value : target.WorkId,
                Stage = db.WorkEvents.Where(e => e.WorkId == issue.WorkId && e.EventType.StartsWith("ROOM_ISSUE_"))
                    .OrderByDescending(e => e.WorkEventId).Select(e => e.EventType).FirstOrDefault() }).ToArrayAsync(ct);
        var ready = new HashSet<long>();
        foreach (var issue in issues)
            if (issue.Stage is "ROOM_ISSUE_INSPECTION_REQUESTED" or "ROOM_ISSUE_REMEDY_REPORTED"
                || issue.Stage == "ROOM_ISSUE_MATERIAL_REQUESTED" && await MaterialDelivered(await CurrentIssueMaterial(issue.WorkId, ct), ct))
                ready.Add(issue.Root);
        return ready.ToArray();
    }
}
