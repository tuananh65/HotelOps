namespace HotelOps.Api.Security.Authorization;

// Shared by authorization and access review. This does not resolve resource ownership.
internal static class PermissionGrantRules
{
    public static bool IsEligible(PermissionGrantPath path, DateTime now) =>
        path.AssignmentActive && path.RoleActive && path.AssignedAt <= now
        && (path.ExpiresAt is null || path.ExpiresAt > now)
        && (path.BranchId is null || (path.BranchActive && path.BranchAccessActive));

    public static string? EffectiveScope(PermissionGrantPath path) => path.DataScope switch
    {
        "ALL" => path.BranchId is null ? "GLOBAL" : "BRANCH",
        "BRANCH" when path.BranchId is not null => "BRANCH",
        "OWN" when path.BranchId is not null => "OWN",
        _ => null
    };
}
