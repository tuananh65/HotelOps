using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Authentication;

public sealed class CredentialLockoutService(
    HotelOpsDbContext db, IPasswordCredentialService passwords, TimeProvider clock)
    : ICredentialLockoutService
{
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = PasswordCredentialService.BaselineIterationCount
        })).HashPassword(new User(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    public async Task<CredentialLockoutResult> VerifyAsync(
        long? userId, string? password,
        Func<User, CancellationToken, Task<bool>> isEligibleForLogin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isEligibleForLogin);
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A caller-owned authentication transaction is required.");
        if (!db.Database.IsSqlServer() || transaction.GetDbTransaction().IsolationLevel
            is not (IsolationLevel.ReadCommitted or IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new InvalidOperationException("SQL Server with locking isolation is required; snapshot is unsupported.");
        EnsureCleanTracker();

        User? user = null;
        if (userId.HasValue)
        {
            // Parameterized SQL; lock the latest database row, never a tracked snapshot.
            var rows = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM dbo.users WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {userId.Value}")
                .AsNoTracking().ToListAsync(cancellationToken);
            user = rows.SingleOrDefault();
        }

        if (user is null)
        {
            passwords.VerifyPassword(new User(), DummyHash.Value, password);
            return new(CredentialLockoutStatus.InvalidCredentials);
        }

        // Verify under the user lock: concurrent reset cannot overtake this result.
        var verification = passwords.VerifyPassword(user, user.PasswordHash, password);
        var verified = verification != PasswordVerificationResult.Failed;
        var snapshotHash = user.PasswordHash;
        var snapshotCount = user.FailedLoginCount;
        var snapshotLock = user.LockedUntil;
        var snapshotTemporaryExpiry = user.TemporaryPasswordExpiresAt;
        var eligible = verified && await isEligibleForLogin(user, cancellationToken);
        EnsureCleanTracker();
        if (db.Database.CurrentTransaction != transaction
            || user.PasswordHash != snapshotHash || user.FailedLoginCount != snapshotCount
            || user.LockedUntil != snapshotLock || user.TemporaryPasswordExpiresAt != snapshotTemporaryExpiry)
            throw new InvalidOperationException("Eligibility callback must not mutate credential state or transaction.");

        if (verified)
        {
            var latest = await db.Users.AsNoTracking()
                .Where(u => u.UserId == user.UserId)
                .Select(u => new { u.PasswordHash, u.FailedLoginCount, u.LockedUntil,
                    u.TemporaryPasswordExpiresAt, u.AccountStatus, u.EmploymentStatus, u.DeletedAt })
                .SingleAsync(cancellationToken);
            if (!string.Equals(latest.PasswordHash, snapshotHash, StringComparison.Ordinal)
                || latest.FailedLoginCount != snapshotCount || latest.LockedUntil != snapshotLock
                || latest.TemporaryPasswordExpiresAt != snapshotTemporaryExpiry
                || latest.AccountStatus != user.AccountStatus || latest.EmploymentStatus != user.EmploymentStatus
                || latest.DeletedAt != user.DeletedAt)
                throw new DbUpdateConcurrencyException("Credential eligibility changed; roll back the authentication transaction.");
        }

        var utcNow = clock.GetUtcNow().UtcDateTime;
        // Non-NULL always remains temporary, including expired credentials. Recheck
        // after the eligibility callback so a deadline crossed during it fails closed.
        if (snapshotTemporaryExpiry.HasValue && snapshotTemporaryExpiry.Value <= utcNow)
            eligible = false;
        var transition = CredentialLockoutPolicy.Evaluate(
            snapshotCount, snapshotLock, verified, eligible, utcNow);

        // Update ONLY these two columns. All credential writers must follow user-first
        // locking and the caller must retain this transaction through login commit.
        if (transition.FailedLoginCount != snapshotCount || transition.LockedUntil != snapshotLock)
        {
            var changed = await db.Users
                .Where(u => u.UserId == user.UserId && u.FailedLoginCount == snapshotCount
                    && u.LockedUntil == snapshotLock && u.PasswordHash == snapshotHash)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.FailedLoginCount, transition.FailedLoginCount)
                    .SetProperty(u => u.LockedUntil, transition.LockedUntil), cancellationToken);
            if (changed != 1)
                throw new DbUpdateConcurrencyException("Credential state changed; roll back the authentication transaction.");
        }

        if (transition.LockStarted)
        {
            var audit = AuthenticationAudit.ForUser(user.UserId, "UPDATE", utcNow,
                new { authenticationEvent = "LOGIN_LOCKOUT", failed_login_count = transition.FailedLoginCount, locked_until = transition.LockedUntil },
                "failed_login_count", "locked_until");
            audit.OldValues = JsonSerializer.Serialize(new { failed_login_count = snapshotCount, locked_until = snapshotLock });
            db.AuditLogs.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            db.Entry(audit).State = EntityState.Detached;
        }

        return new CredentialLockoutResult(transition.Status,
            transition.Status == CredentialLockoutStatus.Locked ? transition.LockedUntil : null,
            transition.Status == CredentialLockoutStatus.Verified
                && verification == PasswordVerificationResult.SuccessRehashNeeded,
            transition.Status == CredentialLockoutStatus.Verified && snapshotTemporaryExpiry.HasValue)
        {
            Proof = transition.Status == CredentialLockoutStatus.Verified
                ? new CredentialVerificationProof(user.UserId, transaction.TransactionId, snapshotHash, snapshotTemporaryExpiry)
                : null
        };
    }

    private void EnsureCleanTracker()
    {
        // Prevent a later SaveChanges (audit or caller) from writing stale User state.
        if (db.ChangeTracker.HasChanges() || db.ChangeTracker.Entries<User>().Any())
            throw new InvalidOperationException("Use a clean context without tracked User entities for credential verification.");
    }
}
