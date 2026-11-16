using HotelOps.Api.Data;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

// Disclosure only. Never use this filtered subset as the completion predicate.
internal static class InternalRequestCompletionVisibility
{
    internal static async Task<InternalRequestCompletionBlocker[]> ReadAsync(HotelOpsDbContext db,
        long user, long branch, IReadOnlyList<InternalRequestCompletionBlocker> blockers, CancellationToken ct)
    {
        var visible = new List<InternalRequestCompletionBlocker>();
        foreach (var group in blockers.GroupBy(b => b.Kind))
        {
            var ids = group.Select(b => b.Id).Distinct().ToArray();
            long[] allowed;
            switch (group.Key)
            {
                case "TASK":
                    var tasks = await InternalRequestTaskLinks.VisibleTasksAsync(db, user, branch, ct);
                    allowed = await tasks.Where(t => ids.Contains(t.TaskId)).Select(t => t.TaskId).ToArrayAsync(ct);
                    break;
                case "CLEANING":
                    var cleaning = await InternalRequestCleaningLinks.VisibleTasksAsync(db, user, branch, ct);
                    allowed = await cleaning.Where(t => ids.Contains(t.CleaningTaskId)).Select(t => t.CleaningTaskId).ToArrayAsync(ct);
                    break;
                case "INCIDENT":
                    var incidents = await IncidentReadAccess.ReadAsync(db, user, branch, ct);
                    allowed = await incidents.Apply(db.Incidents.AsNoTracking()).Where(i => ids.Contains(i.IncidentId))
                        .Select(i => i.IncidentId).ToArrayAsync(ct);
                    break;
                case "MATERIAL":
                    var materials = await MaterialRequestReadAccess.ReadAsync(db, user, branch, DateTime.UtcNow, ct);
                    allowed = await materials.Apply(db.MaterialRequests.AsNoTracking()).Where(m => ids.Contains(m.RequestId))
                        .Select(m => m.RequestId).ToArrayAsync(ct);
                    break;
                // Handover-specific recipient scope is handled by its existing panel.
                // Keep its existence in the generic blocker, without disclosing IDs here.
                default:
                    allowed = [];
                    break;
            }
            visible.AddRange(Filter(group, allowed));
        }
        return visible.OrderBy(b => b.Kind, StringComparer.Ordinal).ThenBy(b => b.Id).ToArray();
    }

    internal static IEnumerable<InternalRequestCompletionBlocker> Filter(
        IEnumerable<InternalRequestCompletionBlocker> blockers, IEnumerable<long> visibleIds)
    {
        var allowed = visibleIds.ToHashSet();
        return blockers.Where(b => b.Code != "LINK_BRANCH_MISMATCH" && allowed.Contains(b.Id));
    }
}
