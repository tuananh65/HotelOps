namespace HotelOps.Api.Services.Authentication;

/// <summary>
/// Technical primitive only; does not authorize account creation or assignments.
/// Caller must save/detach its inserts, own the same context transaction and roll
/// back the entire transaction on any failure. Never perform external delivery inside it.
/// </summary>
public interface IInitialCredentialTransactionParticipant
{
    Task<PendingInitialCredential> StageInitialAsync(PreparedTemporaryCredential prepared,
        long actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>No public secret accessor. Dispose on rollback, cancellation or commit uncertainty.</summary>
public sealed class PendingInitialCredential : IDisposable
{
    private string? _password;
    public DateTime ExpiresAtUtc { get; }

    internal PendingInitialCredential(string password, DateTime expiry)
        => (_password, ExpiresAtUtc) = (password, expiry);

    // Trusted in-assembly orchestration must await successful CommitAsync before calling.
    // This method is not a database commit certificate and must never be called on rollback/unknown outcome.
    internal IssuedTemporaryCredential ReleaseAfterCommit()
        => new(Interlocked.Exchange(ref _password, null)
            ?? throw new InvalidOperationException("Pending credential already released or disposed."), ExpiresAtUtc);

    public void Dispose() => Interlocked.Exchange(ref _password, null);
    public override string ToString() => "[Pending initial credential: redacted]";
}
