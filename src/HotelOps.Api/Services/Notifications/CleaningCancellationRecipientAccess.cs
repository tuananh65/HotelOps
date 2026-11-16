using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// Approved historical receipt read only. Does not grant access to the cancelled task.
internal static class CleaningCancellationRecipientAccess
{
    internal static async Task<bool> AllowsAsync(HotelOpsDbContext db, long user, CleaningCancellationResult r,
        DateTime now, CancellationToken ct)
    {
        if (user <= 0 || r.AssigneeId != user || r.AssignmentId == null) return false;
        var data = new EfPermissionDataSource(db);
        if (!await data.IsActiveUserAsync(user,ct) || !await data.IsActiveBranchAsync(r.BranchId,ct)
            || !await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==user && u.EmploymentStatus=="ACTIVE",ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user && a.BranchId==r.BranchId
                && a.GrantedAt<=now && a.RevokedAt==null,ct)
            || !await db.CleaningAssignments.AsNoTracking().AnyAsync(a=>a.AssignmentId==r.AssignmentId
                && a.AssigneeUserId==user && a.CleaningTaskId==r.Id && a.CleaningTask.Room.BranchId==r.BranchId
                && a.AssignmentStatus=="CANCELLED" && a.EndedAt==r.OccurredAtUtc,ct)) return false;
        return AllowsView(await data.ReadPathsAsync(user,"CLEANING_VIEW",now,ct), r.BranchId, now);
    }

    internal static bool AllowsView(IEnumerable<PermissionGrantPath> paths, long branch, DateTime now) =>
        paths.Any(p=>p.PermissionCode=="CLEANING_VIEW"
            && PermissionGrantRules.IsEligible(p,now) && (PermissionGrantRules.EffectiveScope(p)=="GLOBAL"
                || p.BranchId==branch && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN"));
}
