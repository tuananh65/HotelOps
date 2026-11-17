namespace HotelOps.Api.Services.Users;

// Actor eligibility is independent from permission grants. A role without its own
// grants may still satisfy the actor requirement while another role supplies a grant.
internal sealed record RevokeActorAssignment(long AssignmentId, string Role, long? Branch,
    bool Active, bool RoleActive, DateTime AssignedAt, DateTime? ExpiresAt)
{
    internal bool Effective(DateTime now) => Active && RoleActive && AssignedAt <= now
        && (ExpiresAt == null || ExpiresAt > now);

    internal static bool Has(IReadOnlyList<RevokeActorAssignment> rows, long branch, DateTime now,
        bool allowUnbound, params string[] roles) => rows.Any(r => r.Effective(now)
            && (r.Branch == branch || allowUnbound && r.Branch == null) && roles.Contains(r.Role, StringComparer.Ordinal));
}
