using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal static class RevokeDesignatedResponsibilities
{
    internal static async Task<Dev1ResponsibilityReview> ReadAsync(HotelOpsDbContext db, long target,
        IReadOnlyList<RevokeGrant> before, IReadOnlyList<RevokeGrant> after,
        IReadOnlyList<RevokeActorAssignment> beforeActors, IReadOnlyList<RevokeActorAssignment> afterActors,
        DateTime now, CancellationToken ct)
    {
        var blockers = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive && d.ManagerUserId == target)
            .Select(d => new { d.DepartmentId, d.BranchId }).ToArrayAsync(ct);
        foreach (var department in departments)
        {
            var fact = $"DEPARTMENT_HEAD:{department.DepartmentId}";
            if (!RevokeActorAssignment.Has(beforeActors, department.BranchId, now, false, "DEPT_MANAGER"))
                unresolved.Add(fact + "_ACTOR_UNKNOWN");
            else if (!RevokeActorAssignment.Has(afterActors, department.BranchId, now, false, "DEPT_MANAGER"))
                blockers.Add(fact);
            // Match the explicit UC025 designation prerequisites. This is not a grant
            // of process authority from ManagerUserId, nor a modification of mappings.
            var required = new List<string> { "IR_PROCESS", "MR_APPROVE_L1" };
            if (await db.Warehouses.AnyAsync(w => w.DepartmentId == department.DepartmentId && w.BranchId == department.BranchId && w.IsActive, ct))
                required.Add("MR_APPROVE_FALLBACK");
            foreach (var permission in required)
            {
                bool Has(IReadOnlyList<RevokeGrant> grants) => grants.Any(g => g.RoleCode == "DEPT_MANAGER"
                    && g.Path.BranchId == department.BranchId && g.Path.PermissionCode == permission
                    && PermissionGrantRules.IsEligible(g.Path, now) && PermissionGrantRules.EffectiveScope(g.Path) == "BRANCH");
                if (!Has(before)) unresolved.Add(fact + "_" + permission + "_UNKNOWN");
                else if (!Has(after)) blockers.Add(fact + "_" + permission);
            }
        }
        var defects = await db.InspectionDefects.AsNoTracking()
            .Where(d => d.AssignedTo == target && (d.Status == "OPEN" || d.Status == "IN_PROGRESS"))
            .Select(d => d.DefectId).ToArrayAsync(ct);
        if (defects.Length > 0)
        {
            if (!await HousekeepingSchema.InstalledAsync(db, ct)) unresolved.Add("DEFECT_REWORK_SCHEMA_REQUIRED");
            else foreach (var defect in defects)
            {
                // Rework eligibility is evaluated by RevokeHousekeepingResponsibilities.
                // A legacy standalone defect has no approved executor contract: retain it.
                if (await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.cleaning_rework_defects d WITH(HOLDLOCK)
                    JOIN dbo.cleaning_reworks r WITH(HOLDLOCK) ON r.rework_id=d.rework_id
                    WHERE d.defect_id={defect} AND r.assignee_user_id={target}
                      AND r.workflow_status IN('PENDING_ACCEPTANCE','IN_PROGRESS','ON_HOLD','PENDING_REINSPECTION')
                    """).SingleAsync(ct) != 1)
                    unresolved.Add($"INSPECTION_DEFECT:{defect}_EXECUTOR_CONTRACT_REQUIRED");
            }
        }
        // Asset handover production authorization has not been approved. Reject only
        // affected targets, rather than treating an absent handover as a global blocker.
        var assetHandovers = await db.AssetHandovers.AsNoTracking()
            .Where(h => h.ToUserId == target && h.HandoverStatus == "PENDING")
            .Select(h => h.HandoverId).ToArrayAsync(ct);
        foreach (var id in assetHandovers) unresolved.Add($"ASSET_HANDOVER:{id}_AUTHORITY_REQUIRED");
        return new(blockers.ToArray(), unresolved.ToArray());
    }
}
