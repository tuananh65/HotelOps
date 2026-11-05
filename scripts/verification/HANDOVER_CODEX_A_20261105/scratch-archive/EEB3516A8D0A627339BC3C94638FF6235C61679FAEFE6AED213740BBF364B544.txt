namespace HotelOps.Api.Services.Authentication;

/// <summary>
/// Transaction primitive, not an authorization or reauthentication boundary.
/// The HTTP workflow uses the internal transaction-gated variant, not this raw API.
/// Trusted orchestration must verify actor/session, reset authority, recent
/// reauthentication and employee identity confirmation. Never call the legacy unconditional reset
/// wrapper to implement the administrative endpoint.
/// Raw resets also enforce global-admin preservation and may return LastAdministrator
/// or SerializationUnavailable; they still do not authorize the supplied actor ID.
/// </summary>
public interface IConditionalCredentialResetService
{
    Task<ConditionalCredentialResetResult> ResetAsync(PreparedTemporaryCredential prepared,
        long actorUserId, byte[] expectedVersion, string identityVerificationMethod,
        CancellationToken cancellationToken = default);
}

public enum ConditionalCredentialResetStatus { Success, SelfDenied, NotFound, Inactive, Stale, Forbidden, SessionInvalid, ReauthenticationRequired, LastAdministrator, SerializationUnavailable }

public sealed record ConditionalCredentialResetResult(ConditionalCredentialResetStatus Status,
    IssuedTemporaryCredential? Credential = null, byte[]? Version = null) : IDisposable
{
    public void Dispose() => Credential?.Dispose();
}
