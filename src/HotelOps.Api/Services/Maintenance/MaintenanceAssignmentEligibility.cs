using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceAssignmentEligibility
{
    internal static DateTime RequirePlannedEnd(DateTime? start, DateTime? end)
    {
        if (end is not { Kind: DateTimeKind.Utc } value || start is {} beginning && value < beginning)
            throw new ValidationException("A UTC planned end is required and must not precede planned start.");
        return value;
    }

    internal static async Task RequireAsync(HotelOpsDbContext db, long user, long branch, long work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Owning assignment transaction required.");
        if (user <= 0 || !await MaintenanceExecutionAccess.GrantsAsync(db, user, branch, ct))
            throw new ValidationException("Assignee requires a current Maintenance Staff/Lead execution grant and branch access.");
        var read = await MaintenanceReadAccess.ReadAsync(db, user, branch, ct);
        if (!read.Lead && !read.Staff) throw new ValidationException("Assignee requires current maintenance read permission.");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var leads = await db.UserRoles.AsNoTracking().Where(r => r.BranchId == branch && r.Role.RoleCode == "MAINTENANCE_LEAD"
            && r.IsActive && r.Role.IsActive && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null
            && r.Branch != null && r.Branch.IsActive
            && db.UserBranchAccesses.Any(b => b.UserId == r.UserId && b.BranchId == branch && b.RevokedAt == null && b.GrantedAt <= now))
            .Select(r => r.UserId).Distinct().OrderBy(id => id).ToArrayAsync(ct);
        // A simultaneous Staff role must not evade the Lead segregation rule.
        if (!leads.Contains(user)) return;
        foreach (var reviewer in leads.Where(id => id != user))
            if (!await MaintenanceReviewSeparation.HistoricalPerformanceAsync(db, reviewer, work, ct)
                && await MaintenanceReviewAccess.BranchGrantAsync(db, reviewer, branch, ct)) return;
        throw new ValidationException("Assigning a Maintenance Lead requires another active scoped Lead who can verify the result.");
    }
}
