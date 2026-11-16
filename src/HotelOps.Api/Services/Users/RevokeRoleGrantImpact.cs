using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.InternalRequests;

namespace HotelOps.Api.Services.Users;

public sealed record RevokeGrant(long AssignmentId, PermissionGrantPath Path, string? RoleCode = null);
public enum RevokeCapabilityImpact { Preserved, Lost, Unknown }

/// <summary>Counterfactual grant evaluation, never writer or release authorization.</summary>
public static class RevokeRoleGrantImpact
{
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

    public static RevokeCapabilityImpact InternalRequestHandler(string? state,
        IReadOnlyList<RevokeGrant> before,IReadOnlyList<RevokeGrant> after,long? branch,DateTime nowUtc,
        bool currentHandlerMembershipVerified = false)
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
        // Handler membership is independent of managed-department authority. Only
        // the SQL responsibility reader can establish current active membership.
        if (currentHandlerMembershipVerified && readPreserved && before.Any(Eligible) && after.Any(Eligible)
            && process == RevokeCapabilityImpact.Preserved && read == RevokeCapabilityImpact.Preserved)
            return RevokeCapabilityImpact.Preserved;
        return RevokeCapabilityImpact.Unknown;
    }
}
