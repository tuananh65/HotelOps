using HotelOps.Api.Data;
using HotelOps.Api.Services.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal static class RevokeWorkTimeResponsibilities
{
    internal static async Task<UserResponsibilityAssessment> ReadAsync(HotelOpsDbContext db, long target,
        IReadOnlyList<RevokeGrant> before, IReadOnlyList<RevokeGrant> after,
        IReadOnlyList<RevokeActorAssignment> beforeActors, IReadOnlyList<RevokeActorAssignment> afterActors,
        DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Owning role transaction required.");
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        void Record(RevokeCapabilityImpact impact, string fact)
        {
            if (impact == RevokeCapabilityImpact.Lost) blockers.Add(fact);
            if (impact == RevokeCapabilityImpact.Unknown) unresolved.Add(fact + "_UNKNOWN");
        }
        var workdays = await new WorkdayResponsibilityQueries(db).ReadAsync(target, ct);
        unresolved.UnionWith(workdays.Unresolved);
        var days = await db.Workdays.AsNoTracking().Where(w => w.UserId == target && w.Status == "WORKING")
            .Select(w => new { w.WorkdayId, w.BranchId }).ToArrayAsync(ct);
        foreach (var day in days)
            Record(RevokeRoleGrantImpact.Evaluate(before, after, "ATTENDANCE_RECORD", day.BranchId, true, now), $"WORKDAY:{day.WorkdayId}");
        var shifts = await db.ShiftAssignments.AsNoTracking().Where(a => a.UserId == target && a.AssignmentStatus == "ASSIGNED"
            && a.Shift.Status == "PLANNED" && a.Shift.ScheduledEndAt > now)
            .Select(a => new { a.ShiftAssignmentId, a.Shift.BranchId }).ToArrayAsync(ct);
        foreach (var shift in shifts)
            Record(RevokeRoleGrantImpact.Evaluate(before, after, "ATTENDANCE_RECORD", shift.BranchId, true, now), $"SHIFT:{shift.ShiftAssignmentId}");
        var seriesReview = await TaskSeriesResponsibilities.ReadAsync(db, target, ct);
        unresolved.UnionWith(seriesReview.Unresolved);
        if (seriesReview.Unresolved.Count == 0)
        {
            var series = await TaskSeriesStore.Query(db).Where(s => s.IsActive && s.NextDueAt != null
                && (s.CreatedBy == target || s.DefaultAssigneeId == target)).ToArrayAsync(ct);
            foreach (var row in series)
            {
                var fact = $"TASK_SERIES:{row.SeriesId}";
                if (row.CreatedBy == target)
                {
                    Record(RevokeRoleGrantImpact.Evaluate(before, after, "TASK_MANAGE", row.BranchId, false, now), fact + "_MANAGE");
                    Record(RevokeRoleGrantImpact.Evaluate(before, after, "TASK_VIEW", row.BranchId, false, now), fact + "_MANAGER_READ");
                    Actor(fact + "_MANAGER_ROLE", row.BranchId, ["DEPT_MANAGER"]);
                }
                if (row.DefaultAssigneeId == target)
                {
                    Record(RevokeRoleGrantImpact.Evaluate(before, after, "TASK_EXECUTE", row.BranchId, true, now), fact + "_EXECUTE");
                    Record(RevokeRoleGrantImpact.Evaluate(before, after, "TASK_VIEW", row.BranchId, true, now), fact + "_EXECUTOR_READ");
                    Actor(fact + "_EXECUTOR_ROLE", row.BranchId, ["HK_STAFF", "MAINTENANCE_STAFF", "STOREKEEPER"]);
                    if (!await TaskAssignmentEligibility.InstalledAsync(db, ct)
                        || !await HotelOps.Api.Services.WorkTime.WorkdayMembershipStore.Employees(db, row.BranchId, row.DepartmentId, now)
                            .AnyAsync(u => u.UserId == target, ct)) unresolved.Add(fact + "_MEMBERSHIP_UNAVAILABLE");
                }
                if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == target && a.BranchId == row.BranchId
                    && a.RevokedAt == null && a.GrantedAt <= now && a.Branch.IsActive, ct)) unresolved.Add(fact + "_SCOPE_UNAVAILABLE");
            }
        }
        return new(blockers.ToArray(), unresolved.ToArray());

        void Actor(string fact, long branch, string[] roles)
        {
            var eligible = RevokeActorAssignment.Has(beforeActors, branch, now, true, roles);
            var remaining = RevokeActorAssignment.Has(afterActors, branch, now, true, roles);
            Record(!eligible ? RevokeCapabilityImpact.Unknown : remaining ? RevokeCapabilityImpact.Preserved : RevokeCapabilityImpact.Lost, fact);
        }
    }
}
