namespace HotelOps.Api.Services.Users;

/// <summary>
/// UC-006 approval explicitly matches UC-005 permission/scope composition.
/// Reuses its evaluator and authority locks, not its credential or proof workflow.
/// Does not establish last-admin or open-work safety.
/// </summary>
public sealed class UserAccountStatusAccess(UserCredentialResetAccess approvedScope)
{
    public Task<bool> CanManageAsync(long targetId, CancellationToken ct)
        => approvedScope.CanResetAsync(targetId, ct);

    internal Task LockAuthorityAsync(long actorId, long targetId, CancellationToken ct)
        => approvedScope.LockAuthorityAsync(actorId, targetId, ct);
}
