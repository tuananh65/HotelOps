using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> ListOwnCleaningAsync(SessionReference ticket, long sid, long branch, long? before, CancellationToken ct)
    {
        if (before <= 0) throw new System.ComponentModel.DataAnnotations.ValidationException("Invalid assignment cursor.");
        return BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var rows = await db.CleaningAssignments.AsNoTracking().Where(a => a.AssigneeUserId == ticket.UserId
                && a.AssignmentStatus == "ACTIVE" && a.EndedAt == null && a.AssignedAt <= now
                && (before == null || a.AssignmentId < before) && a.CleaningTask.Room.BranchId == branch
                && a.CleaningTask.Room.IsActive && a.CleaningTask.Status != "COMPLETED" && a.CleaningTask.Status != "CANCELLED")
                .OrderByDescending(a => a.AssignmentId).Take(51)
                .Select(a => new AssignedCleaningHeader(a.CleaningTaskId, branch, a.CleaningTask.RoomId,
                    a.CleaningTask.Room.RoomNumber, a.CleaningTask.TaskNo, a.CleaningTask.Status, a.AssignmentId)).ToArrayAsync(token);
            var page = rows.Take(50).ToArray();
            var visible = new List<AssignedCleaningHeader>();
            foreach (var row in page)
                if (await CanReadAssignedCleaningAsync(ticket.UserId, branch, row.Id, row.AssignmentId, token)) visible.Add(row);
            return new(RoomTypeBatchStatus.Success, new { items = visible, nextCursor = rows.Length > 50 ? (long?)page[^1].AssignmentId : null });
        }, ct, requireRoomTypeSchema: false, featurePermission: async token =>
        {
            var data = new EfPermissionDataSource(db); var now = clock.GetUtcNow().UtcDateTime;
            foreach (var grant in await data.ReadPathsAsync(ticket.UserId, "CLEANING_VIEW", now, token))
                if (grant.PermissionCode == "CLEANING_VIEW" && PermissionGrantRules.IsEligible(grant, now)
                    && (PermissionGrantRules.EffectiveScope(grant) == "GLOBAL"
                        || grant.BranchId == branch && PermissionGrantRules.EffectiveScope(grant) is "BRANCH" or "OWN"))
                    return new PermissionEvaluation(PermissionDecision.Granted);
            return new PermissionEvaluation(PermissionDecision.Denied);
        });
    }
}
