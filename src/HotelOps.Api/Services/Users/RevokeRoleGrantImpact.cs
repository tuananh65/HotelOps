using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;

namespace HotelOps.Api.Services.Users;

public sealed record RevokeGrant(long AssignmentId, PermissionGrantPath Path, string? RoleCode = null);
public enum RevokeCapabilityImpact { Preserved, Lost, Unknown }

/// <summary>Counterfactual grant evaluation, never writer or release authorization.</summary>
public static class RevokeRoleGrantImpact
{
    public static RevokeCapabilityImpact HousekeepingExecutor(string? workflowState, bool rework,
        IReadOnlyList<RevokeGrant> before, IReadOnlyList<RevokeGrant> after, long? branch, DateTime nowUtc)
    {
        if (branch is null or <= 0 || nowUtc.Kind != DateTimeKind.Utc) return RevokeCapabilityImpact.Unknown;
        var open = rework
            ? workflowState is "PENDING_ACCEPTANCE" or "ACCEPTED" or "IN_PROGRESS" or "ON_HOLD" or "PENDING_REINSPECTION"
            : workflowState is "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD" or "PENDING_INSPECTION" or "REWORK_REQUIRED";
        if (!open) return RevokeCapabilityImpact.Unknown;
        var execute = Evaluate(before, after, "CLEANING_EXECUTE", branch, true, nowUtc);
        var read = Evaluate(before, after, "CLEANING_VIEW", branch, true, nowUtc);
        if (execute == RevokeCapabilityImpact.Lost || read == RevokeCapabilityImpact.Lost) return RevokeCapabilityImpact.Lost;
        // Assignee eligibility requires a current HK_STAFF assignment in this branch.
        // A grant on a different actor role does not preserve that eligibility.
        bool Staff(RevokeGrant g) => g.RoleCode == "HK_STAFF" && g.Path.BranchId == branch
            && PermissionGrantRules.IsEligible(g.Path, nowUtc);
        if (before.Any(Staff) && !after.Any(Staff)) return RevokeCapabilityImpact.Lost;
        return before.Any(Staff) && after.Any(Staff) && execute == RevokeCapabilityImpact.Preserved && read == RevokeCapabilityImpact.Preserved
            ? RevokeCapabilityImpact.Preserved : RevokeCapabilityImpact.Unknown;
    }
    public static RevokeCapabilityImpact Evaluate(IReadOnlyList<RevokeGrant> before,
        IReadOnlyList<RevokeGrant> after, string permission, long? branch, bool ownsResource, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || branch is null or <= 0) return RevokeCapabilityImpact.Unknown;
        bool Unknown(IReadOnlyList<RevokeGrant> paths) => paths.Any(g => g.Path.PermissionCode == permission
            && PermissionGrantRules.IsEligible(g.Path, nowUtc) && PermissionGrantRules.EffectiveScope(g.Path) is null);
        bool Has(IReadOnlyList<RevokeGrant> paths) => paths.Any(g => g.Path.PermissionCode == permission
            && PermissionGrantRules.IsEligible(g.Path, nowUtc)
            && (PermissionGrantRules.EffectiveScope(g.Path) == "GLOBAL"
                || g.Path.BranchId == branch && (PermissionGrantRules.EffectiveScope(g.Path) == "BRANCH"
                    || ownsResource && PermissionGrantRules.EffectiveScope(g.Path) == "OWN")));
        if (Unknown(before) || Unknown(after) || !Has(before)) return RevokeCapabilityImpact.Unknown;
        return Has(after) ? RevokeCapabilityImpact.Preserved : RevokeCapabilityImpact.Lost;
    }

    internal static RevokeCapabilityImpact PreventiveExecutor(IReadOnlyList<RevokeGrant> before,
        IReadOnlyList<RevokeGrant> after,long branch,DateTime now)
    {
        if(branch<=0 || now.Kind!=DateTimeKind.Utc)return RevokeCapabilityImpact.Unknown;
        // Reuse the same actor-aware, branch-specific predicates as occurrence execution.
        // An equivalent permission on another role or branch cannot preserve assignment eligibility.
        bool CanExecute(IReadOnlyList<RevokeGrant> grants)=>grants.Any(g=>g.RoleCode!=null
            && Maintenance.MaintenanceExecutionAccess.Allows(g.RoleCode,g.Path,branch,now));
        bool CanRead(IReadOnlyList<RevokeGrant> grants)
        {
            var scope=Maintenance.MaintenanceReadAccess.Resolve(1,branch,grants.Where(g=>g.RoleCode!=null)
                .Select(g=>new Maintenance.MaintenanceReadAccess.Grant(g.RoleCode!,g.Path)),now);
            return scope.Lead || scope.Staff;
        }
        if(before.Concat(after).Any(g=>g.Path.BranchId==branch
            && g.Path.PermissionCode is "MAINTENANCE_VIEW" or "MAINTENANCE_EXECUTE"
            && PermissionGrantRules.IsEligible(g.Path,now)
            && (g.RoleCode==null || PermissionGrantRules.EffectiveScope(g.Path)==null)))return RevokeCapabilityImpact.Unknown;
        if(!CanRead(before) || !CanExecute(before))return RevokeCapabilityImpact.Unknown;
        return CanRead(after) && CanExecute(after)?RevokeCapabilityImpact.Preserved:RevokeCapabilityImpact.Lost;
    }

    public static RevokeCapabilityImpact Maintenance(string? state, bool currentAssignee,
        IReadOnlyList<RevokeGrant> before, IReadOnlyList<RevokeGrant> after, long? branch, DateTime nowUtc)
    {
        if (!currentAssignee || state is "CLOSED" or "CANCELLED") return RevokeCapabilityImpact.Preserved;
        if (state is not ("PENDING" or "IN_PROGRESS" or "ON_HOLD" or "COMPLETED")) return RevokeCapabilityImpact.Unknown;
        return Evaluate(before, after, "MAINTENANCE_EXECUTE", branch, true, nowUtc);
    }

    public static IReadOnlyList<RevokeGrant> Without(IReadOnlyList<RevokeGrant> paths, long assignmentId) =>
        paths.Where(g => g.AssignmentId != assignmentId).ToArray();

    public static RevokeCapabilityImpact OperationalTask(string? workflowState, bool currentAssignee,
        IReadOnlyList<RevokeGrant> before, IReadOnlyList<RevokeGrant> after, long? branch, DateTime nowUtc)
    {
        if (!currentAssignee) return RevokeCapabilityImpact.Preserved;
        // An ACTIVE assignment on a terminal/unassigned or unproved workflow is contradictory,
        // not permission to clear a responsibility. Pending Verification remains open work.
        if (workflowState is not ("ASSIGNED" or "ACCEPTED" or "IN_PROGRESS" or "PAUSED" or "PENDING_VERIFICATION"))
            return RevokeCapabilityImpact.Unknown;
        return Evaluate(before, after, "TASK_EXECUTE", branch, true, nowUtc);
    }

    public static string[] Effective(IReadOnlyList<RevokeGrant> paths, DateTime nowUtc) => paths
        .Where(g => PermissionGrantRules.IsEligible(g.Path, nowUtc))
        .Select(g => $"{g.Path.PermissionCode}/{PermissionGrantRules.EffectiveScope(g.Path) ?? "UNKNOWN"}/{g.Path.BranchId}")
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    // Audit summaries intentionally collapse equivalent permissions. Final rereads
    // must also preserve their assignment/actor provenance and validity window.
    internal static bool SameEffectivePaths(IReadOnlyList<RevokeGrant> expected,
        IReadOnlyList<RevokeGrant> actual, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) return false;
        return expected.Where(g => PermissionGrantRules.IsEligible(g.Path, nowUtc)).ToHashSet()
            .SetEquals(actual.Where(g => PermissionGrantRules.IsEligible(g.Path, nowUtc)));
    }

    public static RevokeCapabilityImpact InternalRequestHandler(string? state,
        IReadOnlyList<RevokeGrant> before,IReadOnlyList<RevokeGrant> after,long? branch,DateTime nowUtc,
        bool currentHandlerMembershipVerified = false, bool currentManagerMappingVerified = false)
    {
        if(state!="IN_PROGRESS")return RevokeCapabilityImpact.Unknown;
        // Current handler mutation independently requires both permissions. Losing
        // either is sufficient positive evidence; surviving grants do not prove
        // allowed actor roles, current department eligibility or mapping.
        var process=Evaluate(before,after,"IR_PROCESS",branch,true,nowUtc);
        var read=Evaluate(before,after,"IR_VIEW",branch,true,nowUtc);
        if (process == RevokeCapabilityImpact.Lost || read == RevokeCapabilityImpact.Lost)
            return RevokeCapabilityImpact.Lost;
        if (branch is null or <= 0 || nowUtc.Kind != DateTimeKind.Utc)
            return RevokeCapabilityImpact.Unknown;
        // Match UC123 actor-aware read scope. A global IR_VIEW grant on an
        // ineligible role does not replace the current handler's scoped read access.
        var readPreserved = false;
        if (!before.Concat(after).Any(g => g.Path.PermissionCode == "IR_VIEW" && g.RoleCode is null))
        {
            bool CanRead(IReadOnlyList<RevokeGrant> grants)
            {
                var scope = InternalRequestReadAccess.Resolve(1, grants.Where(g => g.RoleCode != null)
                    .Select(g => new InternalRequestReadAccess.Grant(g.RoleCode!, g.Path)), nowUtc);
                // Ownership is established by the caller's unique current-handler query.
                return scope.Global || scope.Branches.Contains(branch.Value) || scope.RelatedBranches.Contains(branch.Value);
            }
            if (CanRead(before) && !CanRead(after)) return RevokeCapabilityImpact.Lost;
            readPreserved = CanRead(before) && CanRead(after);
        }
        // A permission on an ineligible actor role cannot replace a handler's role.
        // Missing role provenance cannot establish loss; keep legacy callers fail-closed.
        if (before.Concat(after).Any(g => g.Path.PermissionCode == "IR_PROCESS" && g.RoleCode is null))
            return RevokeCapabilityImpact.Unknown;
        bool Eligible(RevokeGrant grant) => grant.RoleCode is {} role
            && InternalRequestProgressAuthority.EligibleHandler(role, grant.Path, branch.Value, nowUtc);
        if (before.Any(Eligible) && !after.Any(Eligible)) return RevokeCapabilityImpact.Lost;
        bool ProvenAuthority(RevokeGrant grant) => currentHandlerMembershipVerified && Eligible(grant)
            || currentManagerMappingVerified && grant.RoleCode is {} role
                && InternalRequestProcessAuthority.Eligible(role, grant.Path, branch.Value, nowUtc);
        if (before.Any(ProvenAuthority) && !after.Any(ProvenAuthority)) return RevokeCapabilityImpact.Lost;
        // Handler membership is independent of managed-department authority. Only
        // the SQL responsibility reader can establish current active membership.
        if (readPreserved && before.Any(ProvenAuthority) && after.Any(ProvenAuthority)
            && process == RevokeCapabilityImpact.Preserved && read == RevokeCapabilityImpact.Preserved)
            return RevokeCapabilityImpact.Preserved;
        return RevokeCapabilityImpact.Unknown;
    }
}
