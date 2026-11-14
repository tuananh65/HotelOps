using HotelOps.Api.Security.Authorization;

namespace HotelOps.Api.Services.Users;

public sealed record RevokeGrant(long AssignmentId, PermissionGrantPath Path);
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

    public static string[] Effective(IReadOnlyList<RevokeGrant> paths, DateTime nowUtc) => paths
        .Where(g => PermissionGrantRules.IsEligible(g.Path, nowUtc))
        .Select(g => $"{g.Path.PermissionCode}/{PermissionGrantRules.EffectiveScope(g.Path) ?? "UNKNOWN"}/{g.Path.BranchId}")
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
