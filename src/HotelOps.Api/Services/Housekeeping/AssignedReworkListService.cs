using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record AssignedReworkHeader(long Id, long BranchId, string RoomNumber, string TaskNumber, string RecordedStatus);
public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ListOwnReworksAsync(SessionReference ticket, long sid, long branch, long? before, CancellationToken ct)
    {
        if (before <= 0) throw new ValidationException("Invalid assignment cursor.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var rows = await db.CleaningReworks.AsNoTracking().Where(w => w.AssigneeUserId == ticket.UserId
                && w.CleaningTask.Room.BranchId == branch && w.CleaningTask.Room.IsActive
                && (before == null || w.ReworkId < before) && w.Status != "COMPLETED" && w.Status != "CANCELLED")
                .OrderByDescending(w => w.ReworkId).Take(51)
                .Select(w => new AssignedReworkHeader(w.ReworkId, branch, w.CleaningTask.Room.RoomNumber,
                    w.CleaningTask.TaskNo, w.Status)).ToArrayAsync(token);
            var page = rows.Take(50).ToArray();
            var visible = new List<AssignedReworkHeader>();
            foreach (var row in page)
                if ((await ReworkAccess(ticket.UserId, branch, row.Id, "CLEANING_VIEW", token)).IsGranted) visible.Add(row);
            return new(RoomTypeBatchStatus.Success, new { items = visible, nextCursor = rows.Length > 50 ? (long?)page[^1].Id : null });
        }, ct, requireRoomTypeSchema: false, featurePermission: async token =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var paths = await new EfPermissionDataSource(db).ReadPathsAsync(ticket.UserId, "CLEANING_VIEW", now, token);
            return new PermissionEvaluation(paths.Any(p => PermissionGrantRules.IsEligible(p, now)
                && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL" || p.BranchId == branch
                    && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN")) ? PermissionDecision.Granted : PermissionDecision.Denied);
        });
    }
}
