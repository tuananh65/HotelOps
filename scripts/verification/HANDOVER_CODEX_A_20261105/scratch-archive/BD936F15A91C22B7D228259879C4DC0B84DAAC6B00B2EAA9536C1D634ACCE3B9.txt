using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Authentication;

public interface ICredentialLockoutService
{
    /// <summary>
    /// Requires a caller-owned SQL transaction and a clean ChangeTracker. Acquire
    /// the user lock before any session lock. The eligibility callback must only
    /// read current account/temporary-password state, not write or commit.
    /// Commit ordinary failures to persist counters; roll back on exceptions.
    /// Verified is provisional: complete login/session work before committing the
    /// SAME transaction. Never issue a cookie before commit.
    /// </summary>
    Task<CredentialLockoutResult> VerifyAsync(
        long? userId, string? password,
        Func<User, CancellationToken, Task<bool>> isEligibleForLogin,
        CancellationToken cancellationToken = default);
}

public enum CredentialLockoutStatus
{
    InvalidCredentials,
    Locked,
    Verified
}

public sealed record CredentialLockoutResult(
    CredentialLockoutStatus Status,
    DateTime? LockedUntil = null,
    bool RehashNeeded = false,
    bool MustChangePassword = false)
{
    internal CredentialVerificationProof? Proof { get; init; }
}

internal sealed class CredentialVerificationProof(long userId, Guid transactionId, string? hash, DateTime? temporaryExpiry)
{
    internal long UserId { get; } = userId;
    internal Guid TransactionId { get; } = transactionId;
    internal string? Hash { get; } = hash;
    internal DateTime? TemporaryExpiry { get; } = temporaryExpiry;
    private int _consumed;
    internal bool TryConsume() => Interlocked.Exchange(ref _consumed, 1) == 0;
    public override string ToString() => "[Credential verification proof: redacted]";
}
