namespace HotelOps.Api.Services.Authentication;

/// <summary>Pure state transitions; storage/serialization belongs to the service.</summary>
public static class CredentialLockoutPolicy
{
    public const int FailureThreshold = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(30);

    public static CredentialLockoutTransition Evaluate(
        int failedCount, DateTime? lockedUntil, bool passwordVerified,
        bool eligibleForLogin, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("UTC time is required.", nameof(utcNow));
        if (failedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(failedCount));

        if (lockedUntil > utcNow)
        {
            // Wrong passwords cannot discover the lock or extend its deadline.
            var status = passwordVerified && eligibleForLogin
                ? CredentialLockoutStatus.Locked : CredentialLockoutStatus.InvalidCredentials;
            return new(status, failedCount, lockedUntil, false);
        }

        if (passwordVerified)
        {
            return eligibleForLogin
                ? new(CredentialLockoutStatus.Verified, 0, null, false)
                : new(CredentialLockoutStatus.InvalidCredentials, failedCount, lockedUntil, false);
        }

        // An expired lock starts a fresh failure cycle (including now == deadline).
        var count = lockedUntil.HasValue ? 1 : Math.Min(failedCount, FailureThreshold - 1) + 1;
        var startsLock = count >= FailureThreshold;
        return new(CredentialLockoutStatus.InvalidCredentials, count,
            startsLock ? utcNow.Add(LockDuration) : null, startsLock);
    }
}

public sealed record CredentialLockoutTransition(
    CredentialLockoutStatus Status, int FailedLoginCount, DateTime? LockedUntil,
    bool LockStarted);
