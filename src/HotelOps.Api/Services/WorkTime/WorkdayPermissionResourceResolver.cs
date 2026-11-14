using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

/// <summary>Approved B1.1 own-only bindings. Existing cleaning semantics are delegated unchanged.</summary>
public sealed class WorkdayPermissionResourceResolver(HotelOpsDbContext db,
    CleaningTaskResourceResolver cleaning) : IPermissionResourceResolver
{
    public async Task<VerifiedPermissionResource?> ResolveAsync(string permissionCode,
        PermissionResource resource, long currentUserId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (resource is null) return null;
        if (resource.ResourceType is not ("workday_self_branch" or "workday" or "shift_assignment"))
            return await cleaning.ResolveAsync(permissionCode, resource, currentUserId, cancellationToken);
        if (currentUserId <= 0 || resource.ResourceId <= 0) return null;
        var ct = cancellationToken;
        long? branch;
        switch (resource.ResourceType)
        {
            case "workday_self_branch":
                if (permissionCode is not ("ATTENDANCE_RECORD" or "SHIFT_VIEW")) return null;
                branch = resource.ResourceId;
                break;
            case "workday":
                if (permissionCode != "ATTENDANCE_RECORD") return null;
                branch = await db.Workdays.AsNoTracking().Where(d => d.WorkdayId == resource.ResourceId
                    && d.UserId == currentUserId).Select(d => (long?)d.BranchId).SingleOrDefaultAsync(ct);
                break;
            default:
                if (permissionCode is not ("ATTENDANCE_RECORD" or "SHIFT_VIEW")) return null;
                branch = await db.ShiftAssignments.AsNoTracking().Where(a => a.ShiftAssignmentId == resource.ResourceId
                    && a.UserId == currentUserId).Select(a => (long?)a.Shift.BranchId).SingleOrDefaultAsync(ct);
                break;
        }
        if (branch is null || branch <= 0) return null;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.UserId == currentUserId
                && u.AccountStatus == "ACTIVE" && u.EmploymentStatus == "ACTIVE" && u.DeletedAt == null, ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == currentUserId
                && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive, ct))
            return null;
        // An adapter refusal happens BEFORE grant scope evaluation, including BRANCH/ALL paths.
        return new(branch.Value, HasOwnership: true);
    }
}
