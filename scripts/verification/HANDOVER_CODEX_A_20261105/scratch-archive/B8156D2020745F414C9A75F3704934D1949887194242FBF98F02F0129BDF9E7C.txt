namespace HotelOps.Api.Services.Authentication;

public interface ITemporaryCredentialService
{
    PreparedTemporaryCredential Prepare(long userId);
    TemporaryCredentialState GetState(DateTime? expiresAt);

    /// <summary>
    /// Caller must authorize the actor/target and secure the one-time handoff.
    /// Owns/commits its transaction; do not wrap in an existing transaction or
    /// wait for external delivery inside it. A failed attempt consumes preparation.
    /// AdminReset participates in global-admin serialization and throws
    /// GlobalAdministrationInvariantException if it would remove the last durable admin.
    /// This primitive does not replace caller authorization/revalidation.
    /// </summary>
    Task<IssuedTemporaryCredential> ApplyAsync(PreparedTemporaryCredential prepared,
        long actorUserId, CredentialProvisioningOperation operation,
        CancellationToken cancellationToken = default);
}

public enum TemporaryCredentialState { Regular, TemporaryValid, TemporaryExpired }
public enum CredentialProvisioningOperation { InitialProvisioning, AdminReset }

/// <summary>In-memory preparation only. Never serialize, log or cache this object.</summary>
public sealed class PreparedTemporaryCredential : IDisposable
{
    private string? _password;
    private string? _hash;
    private int _consumed;
    internal long UserId { get; }

    internal PreparedTemporaryCredential(long userId, string password, string hash)
        => (UserId, _password, _hash) = (userId, password, hash);

    internal (string Password, string Hash) Consume()
    {
        if (Interlocked.Exchange(ref _consumed, 1) != 0)
            throw new InvalidOperationException("Credential preparation is already consumed or disposed.");
        var result = (_password!, _hash!);
        _password = null;
        _hash = null;
        return result;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _consumed, 1) == 0)
        {
            _password = null;
            _hash = null;
        }
    }

    public override string ToString() => "[Prepared temporary credential: redacted]";
}

/// <summary>
/// Returned only after commit. TakePasswordOnce is for a future authorized secure
/// handoff only: never log/serialize the returned string or put it in URLs, files,
/// telemetry or an ordinary DTO. Losing delivery requires a new explicit reset.
/// </summary>
public sealed class IssuedTemporaryCredential : IDisposable
{
    private string? _password;
    public DateTime ExpiresAtUtc { get; }

    internal IssuedTemporaryCredential(string password, DateTime expiresAtUtc)
        => (_password, ExpiresAtUtc) = (password, expiresAtUtc);

    public string TakePasswordOnce() => Interlocked.Exchange(ref _password, null)
        ?? throw new InvalidOperationException("Temporary password is already taken or disposed.");

    public void Dispose() => Interlocked.Exchange(ref _password, null);
    public override string ToString() => "[Issued temporary credential: redacted]";
}
