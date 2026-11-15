using HotelOps.Api.Data;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal sealed record InspectionFailureIntent(int SchemaVersion, string Kind, long[] SupervisorIds,
    CleanerAttribution OriginalCleaner)
{
    public long[] Recipients => SupervisorIds.Append(OriginalCleaner.CleanerUserId).Distinct().Order().ToArray();
}

internal static class InspectionFailureRecipients
{
    internal static async Task<long[]> SupervisorsAsync(HotelOpsDbContext db, long branch, DateTime now, CancellationToken ct)
    {
        var candidates = await db.UserRoles.AsNoTracking().Where(r => r.IsActive && r.Role.IsActive
            && r.Role.RoleCode == "HK_SUPERVISOR" && (r.BranchId == branch || r.BranchId == null)
            && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null
            && db.UserBranchAccesses.Any(a => a.UserId == r.UserId && a.BranchId == branch
                && a.RevokedAt == null && a.GrantedAt <= now))
            .Select(r => r.UserId).Distinct().Order().ToArrayAsync(ct);
        var eligible = new List<long>();
        foreach (var id in candidates)
            if (await InspectionRecipientReadAccess.CanReadAsync(db, id, branch, now, ct)) eligible.Add(id);
        return eligible.ToArray();
    }
}
