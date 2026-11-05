using System.Data;
using System.Security.Cryptography;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Authentication;

public sealed class TemporaryCredentialService(
    HotelOpsDbContext db, IPasswordCredentialService passwords,
    ISessionRevocationService sessions, TimeProvider clock) : ITemporaryCredentialService, IInitialCredentialTransactionParticipant
{
    public static readonly TimeSpan Validity = TimeSpan.FromHours(24);
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    public PreparedTemporaryCredential Prepare(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        // 24 independently sampled symbols from a 64-character alphabet (144 bits
        // before the tiny rejection for composition); no fixed suffix/password.
        string password;
        do
        {
            password = string.Create(24, Alphabet, static (chars, alphabet) =>
            {
                for (var i = 0; i < chars.Length; i++)
                    chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            });
        }
        while (!password.Any(char.IsUpper) || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit) || !password.Any(c => c is '-' or '_'));

        var hash = passwords.HashPassword(new User { UserId = userId }, password);
        return new PreparedTemporaryCredential(userId, password, hash);
    }

    public TemporaryCredentialState GetState(DateTime? expiresAt)
    {
        if (!expiresAt.HasValue) return TemporaryCredentialState.Regular;
        // SQL datetime2 is UTC by contract even when materialized as Unspecified.
        if (expiresAt.Value.Kind == DateTimeKind.Local)
            throw new ArgumentException("Expiry must be UTC or SQL UTC datetime2.", nameof(expiresAt));
        return expiresAt.Value > clock.GetUtcNow().UtcDateTime
            ? TemporaryCredentialState.TemporaryValid : TemporaryCredentialState.TemporaryExpired;
    }

    public async Task<IssuedTemporaryCredential> ApplyAsync(PreparedTemporaryCredential prepared,
        long actorUserId, CredentialProvisioningOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actorUserId);
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null
            || System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("Use SQL Server without an existing/ambient transaction for credential issuance.");
        if (db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Credential issuance requires a clean context.");

        // Consumed before opening a transaction: no user interaction while locked.
        var material = prepared.Consume();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var expiry = await ApplyCoreAsync(prepared.UserId, material.Hash, actorUserId, operation, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new IssuedTemporaryCredential(material.Password, expiry);
    }

    public async Task<PendingInitialCredential> StageInitialAsync(PreparedTemporaryCredential prepared,
        long actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actorUserId);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction == null
            || System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("Initial credential participant requires an explicit SQL Server caller transaction.");
        if (db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Save and detach caller entries before staging credentials.");
        var material = prepared.Consume();
        var expiry = await ApplyCoreAsync(prepared.UserId, material.Hash, actorUserId,
            CredentialProvisioningOperation.InitialProvisioning, cancellationToken);
        // No commit and no public plaintext accessor. Trusted orchestration releases only after commit.
        return new PendingInitialCredential(material.Password, expiry);
    }

    private async Task<DateTime> ApplyCoreAsync(long userId, string hash, long actorUserId,
        CredentialProvisioningOperation operation, CancellationToken cancellationToken)
    {
        AuditLog? pendingAudit = null;
        try
        {
            var rows = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM dbo.users WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {userId}")
                .AsNoTracking().ToListAsync(cancellationToken);
            var user = rows.SingleOrDefault()
                ?? throw new InvalidOperationException("Credential target was not found.");
            if (user.DeletedAt != null)
                throw new InvalidOperationException("Cannot issue credentials to a deleted account.");
            if (operation == CredentialProvisioningOperation.InitialProvisioning
                && (user.PasswordHash != null || user.TemporaryPasswordExpiresAt != null))
                throw new InvalidOperationException("Existing credentials require explicit reset, not initial provisioning.");

            var now = clock.GetUtcNow().UtcDateTime;
            var expiry = now.Add(Validity);
            var updated = await db.Users.Where(u => u.UserId == user.UserId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.PasswordHash, hash)
                    .SetProperty(u => u.TemporaryPasswordExpiresAt, expiry)
                    .SetProperty(u => u.FailedLoginCount, 0)
                    .SetProperty(u => u.LockedUntil, (DateTime?)null), cancellationToken);
            if (updated != 1) throw new DbUpdateConcurrencyException("Credential update did not affect exactly one user.");

            var revokedCount = await sessions.RevokeAllAsync(user.UserId, now, cancellationToken);
            pendingAudit = AuthenticationAudit.ForUser(user.UserId, "UPDATE", now,
                new { authenticationEvent = operation == CredentialProvisioningOperation.AdminReset ? "CREDENTIAL_RESET" : "CREDENTIAL_PROVISION",
                    temporary_password_expires_at = expiry, failed_login_count = 0, locked_until = (DateTime?)null, revoked_session_count = revokedCount },
                "password_hash", "temporary_password_expires_at", "failed_login_count", "locked_until");
            pendingAudit.ActorUserId = actorUserId;
            db.AuditLogs.Add(pendingAudit);
            await db.SaveChangesAsync(cancellationToken);
            return expiry;
        }
        finally
        {
            if (pendingAudit is not null)
                db.Entry(pendingAudit).State = EntityState.Detached;
        }
    }
}
