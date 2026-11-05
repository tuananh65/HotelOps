using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;

namespace HotelOps.Api.Services.Users;

public sealed class AdminCredentialResetWorkflow(ITemporaryCredentialService credentials,
    UserCredentialResetAccess access, ICurrentUserAccessor current, AdminReauthenticationService reauthentication)
{
    public async Task<ConditionalCredentialResetResult> ResetAsync(SessionReference reference, long sessionId,
        long targetId, byte[] expectedVersion, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != reference.UserId || actor.SessionId != sessionId)
            return new(ConditionalCredentialResetStatus.SessionInvalid);
        if (actor.IsRestricted) return new(ConditionalCredentialResetStatus.Forbidden);
        using var prepared = credentials.Prepare(targetId);
        // The concrete scoped instance owns the same context as both security checks.
        return await ((TemporaryCredentialService)credentials).ResetWithGateAsync(prepared, actor.UserId, expectedVersion,
            "INTERNAL_PROCEDURE_CONFIRMATION", async token =>
            {
                await access.LockAuthorityAsync(actor.UserId, targetId, token);
                var proof = await reauthentication.ValidateProofAsync(reference, sessionId, token);
                if (proof == ReauthenticationStatus.SessionInvalid) return ConditionalCredentialResetStatus.SessionInvalid;
                if (proof != ReauthenticationStatus.Succeeded) return ConditionalCredentialResetStatus.ReauthenticationRequired;
                if (!await access.CanResetAsync(targetId, token)) return ConditionalCredentialResetStatus.Forbidden;
                return null;
            }, ct);
    }
}
