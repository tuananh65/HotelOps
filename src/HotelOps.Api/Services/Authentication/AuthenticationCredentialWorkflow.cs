using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Authentication;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Authentication;

public sealed class AuthenticationCredentialWorkflow(HotelOpsDbContext db, ISqlSessionService sessions,
    ISessionRevocationService revocations, IPasswordCredentialService passwords, TimeProvider clock)
    : IAuthenticationCredentialWorkflow
{
    public async Task LogoutAsync(SessionReference? reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reference is null) return; // No usable credential; middleware already established this state.
        await RunAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            // False means SQL confirmed missing/already revoked; both are idempotent success.
            await sessions.RevokeSessionAsync(reference, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(SessionReference reference, long sessionId,
        ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!PasswordChangePolicy.HasValidInput(request)) return new(ChangePasswordStatus.ValidationFailed);
        if (!PasswordChangePolicy.IsStrongEnough(request.NewPassword)) return new(ChangePasswordStatus.PolicyViolation);

        try
        {
            return await RunAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                // Validate acquires users UPDLOCK,HOLDLOCK before the session lock; keep both until commit.
                var valid = await sessions.ValidateSessionAsync(reference, cancellationToken);
                if (!Matches(valid, reference, sessionId)) return new ChangePasswordResult(ChangePasswordStatus.SessionInvalid);
                var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == reference.UserId, cancellationToken);
                if (passwords.VerifyPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
                    return new ChangePasswordResult(ChangePasswordStatus.CurrentPasswordInvalid);
                if (passwords.VerifyPassword(user, user.PasswordHash, request.NewPassword) != PasswordVerificationResult.Failed)
                    return new ChangePasswordResult(ChangePasswordStatus.PolicyViolation);

                var newHash = passwords.HashPassword(user, request.NewPassword);
                // Time can advance while hashing: expired temporary/session cannot be promoted.
                var latest = await sessions.ValidateSessionAsync(reference, cancellationToken);
                if (!Matches(latest, reference, sessionId)) return new ChangePasswordResult(ChangePasswordStatus.SessionInvalid);
                var current = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == reference.UserId, cancellationToken);
                if (!string.Equals(current.PasswordHash, user.PasswordHash, StringComparison.Ordinal)
                    || current.TemporaryPasswordExpiresAt != user.TemporaryPasswordExpiresAt
                    || current.AccountStatus != user.AccountStatus || current.DeletedAt != user.DeletedAt)
                    return new ChangePasswordResult(ChangePasswordStatus.StateChanged);

                var now = clock.GetUtcNow().UtcDateTime;
                if (latest.Session!.ExpiresAtUtc <= now || latest.Session.AbsoluteExpiresAtUtc <= now
                    || current.TemporaryPasswordExpiresAt <= now)
                    return new ChangePasswordResult(ChangePasswordStatus.SessionInvalid);
                var changed = await db.Users.Where(u => u.UserId == user.UserId && u.PasswordHash == user.PasswordHash
                        && u.TemporaryPasswordExpiresAt == user.TemporaryPasswordExpiresAt
                        && u.AccountStatus == user.AccountStatus && u.DeletedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, newHash)
                        .SetProperty(u => u.TemporaryPasswordExpiresAt, (DateTime?)null)
                        .SetProperty(u => u.FailedLoginCount, 0).SetProperty(u => u.LockedUntil, (DateTime?)null)
                        .SetProperty(u => u.UpdatedAt, now), cancellationToken);
                if (changed != 1) throw new DbUpdateConcurrencyException("Credential state changed during password change.");

                // One shared revoke-all implementation; no separate copy of revocation SQL.
                var revoked = await revocations.RevokeAllAsync(user.UserId, now, cancellationToken);
                // Internal proof can only be minted after verified current password + valid session,
                // credential mutation and revoke-all, while retaining the same user lock/transaction.
                var proof = new CredentialLockoutResult(CredentialLockoutStatus.Verified)
                { Proof = new CredentialVerificationProof(user.UserId, transaction.TransactionId, newHash, null) };
                var created = await sessions.CreateSessionAsync(proof, cancellationToken);
                if (created.Session.IsRestricted || created.Session.SessionId == sessionId
                    || string.Equals(created.Ticket.ExportCredentialForProtectedTicket(),
                        reference.ExportCredentialForProtectedTicket(), StringComparison.Ordinal))
                    throw new InvalidOperationException("Password change must create a fresh normal session.");

                var audit = AuthenticationAudit.ForUser(user.UserId, "UPDATE", now,
                    new { authenticationEvent = "PASSWORD_CHANGE", revoked_session_count = revoked, temporary_password_expires_at = (DateTime?)null },
                    "password_hash", "temporary_password_expires_at", "failed_login_count", "locked_until", "updated_at");
                audit.ActorUserId = user.UserId;
                db.AuditLogs.Add(audit);
                try { await db.SaveChangesAsync(cancellationToken); }
                finally { db.Entry(audit).State = EntityState.Detached; }
                await transaction.CommitAsync(cancellationToken);
                return new ChangePasswordResult(ChangePasswordStatus.Succeeded,
                    new(user.UserId.ToString(CultureInfo.InvariantCulture), user.Username, user.FullName), created);
            }, cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return new(ChangePasswordStatus.StateChanged); }
    }

    private static bool Matches(SessionValidationResult result, SessionReference reference, long sessionId)
        => result.IsValid && result.Session is { } s && s.SessionId == sessionId && s.UserId == reference.UserId
            && s.IsRestricted == reference.IsRestricted && s.CreatedAtUtc == reference.CreatedAtUtc;

    private async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null
            || System.Transactions.Transaction.Current != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Credential workflow requires a clean SQL Server context without an existing transaction.");
        try { return await work(); }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not SessionStoreUnavailableException
            && SessionStoreUnavailableException.Matches(e)) { throw new SessionStoreUnavailableException(e); }
    }
}
