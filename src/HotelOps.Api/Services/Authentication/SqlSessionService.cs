using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Authentication;

public sealed class SqlSessionService(HotelOpsDbContext db, ISessionRevocationService revocations, TimeProvider clock)
    : ISqlSessionService
{
    public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult verifiedCredential, CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            var proof = verifiedCredential.Proof;
            if (verifiedCredential.Status != CredentialLockoutStatus.Verified || proof is null
                || proof.TransactionId != db.Database.CurrentTransaction!.TransactionId || !proof.TryConsume())
                throw new InvalidOperationException("A fresh, single-use credential proof in the same transaction is required.");
            var user = await LockUserAsync(proof.UserId, cancellationToken)
                ?? throw new InvalidOperationException("Session user no longer exists.");
            var now = clock.GetUtcNow().UtcDateTime;
            if (!SqlSessionPolicy.IsUserActive(user)
                || !string.Equals(user.PasswordHash, proof.Hash, StringComparison.Ordinal)
                || user.TemporaryPasswordExpiresAt != proof.TemporaryExpiry
                || user.LockedUntil > now || user.TemporaryPasswordExpiresAt <= now)
                throw new InvalidOperationException("Credential state changed or is ineligible for session creation.");

            var restricted = user.TemporaryPasswordExpiresAt.HasValue;
            var absolute = SqlSessionPolicy.AbsoluteDeadline(now, user.TemporaryPasswordExpiresAt);
            var idle = now.Add(restricted ? SqlSessionPolicy.RestrictedLifetime : SqlSessionPolicy.NormalIdleTimeout);
            var raw = SessionCredential.Generate();
            var row = new UserSession { UserId = user.UserId, SessionKeyHash = SessionCredential.Hash(raw),
                CreatedAt = now, LastUsedAt = now, ExpiresAt = idle < absolute ? idle : absolute };
            var audit = Audit(user.UserId, "SESSION_CREATE", now);
            db.UserSessions.Add(row); db.AuditLogs.Add(audit);
            try { await db.SaveChangesAsync(cancellationToken); }
            finally { db.Entry(row).State = EntityState.Detached; db.Entry(audit).State = EntityState.Detached; }
            var ticket = SessionReference.FromProtectedTicket(user.UserId, raw, restricted, now);
            return new CreatedSqlSession(ticket, new(row.SessionId, user.UserId, now, row.ExpiresAt, absolute, restricted));
        }, cancellationToken);

    public Task<SessionValidationResult> ValidateSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default)
        => CheckAsync(ticket, false, cancellationToken);

    public Task<SessionValidationResult> TouchSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default)
        => CheckAsync(ticket, true, cancellationToken);

    private Task<SessionValidationResult> CheckAsync(SessionReference ticket, bool touch, CancellationToken ct)
        => RunAsync(async () =>
        {
            var user = await LockUserAsync(ticket.UserId, ct);
            if (user is null) return new SessionValidationResult(false);
            var row = await LockSessionAsync(ticket, ct);
            if (row is null) return new SessionValidationResult(false);
            var now = clock.GetUtcNow().UtcDateTime;
            var valid = SqlSessionPolicy.Validate(user, row, ticket, now);
            if (valid is null) return new SessionValidationResult(false);
            if (!touch) return new SessionValidationResult(true, valid);
            var window = SqlSessionPolicy.TouchWindow(row, valid, now);
            var changed = await db.UserSessions.Where(s => s.SessionId == row.SessionId
                && s.UserId == ticket.UserId && s.SessionKeyHash == row.SessionKeyHash
                && s.RevokedAt == null && s.ExpiresAt == row.ExpiresAt && s.ExpiresAt > now
                && s.LastUsedAt == row.LastUsedAt && s.CreatedAt == row.CreatedAt)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.LastUsedAt, window.LastUsedAt)
                    .SetProperty(s => s.ExpiresAt, window.ExpiresAt), ct);
            if (changed != 1) throw new DbUpdateConcurrencyException("Session changed; roll back the transaction.");
            return new SessionValidationResult(true, valid with { ExpiresAtUtc = window.ExpiresAt });
        }, ct);

    public Task<bool> RevokeSessionAsync(SessionReference ticket, CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            if (await LockUserAsync(ticket.UserId, cancellationToken) is null) return false;
            var row = await LockSessionAsync(ticket, cancellationToken);
            if (row is null || row.RevokedAt.HasValue) return false;
            var now = clock.GetUtcNow().UtcDateTime;
            var changed = await db.UserSessions.Where(s => s.SessionId == row.SessionId
                && s.UserId == ticket.UserId && s.SessionKeyHash == row.SessionKeyHash && s.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.RevokedAt, now), cancellationToken);
            if (changed != 1) throw new DbUpdateConcurrencyException("Session revocation state changed.");
            await WriteAuditAsync(ticket.UserId, "SESSION_REVOKE", now, cancellationToken);
            return true;
        }, cancellationToken);

    public Task<int> RevokeAllSessionsAsync(long userId, CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            if (await LockUserAsync(userId, cancellationToken) is null) return 0;
            var now = clock.GetUtcNow().UtcDateTime;
            var count = await revocations.RevokeAllAsync(userId, now, cancellationToken);
            if (count > 0) await WriteAuditAsync(userId, "SESSION_REVOKE_ALL", now, cancellationToken);
            return count;
        }, cancellationToken);

    private async Task<User?> LockUserAsync(long userId, CancellationToken ct)
        => (await db.Users.FromSqlInterpolated($"SELECT * FROM dbo.users WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {userId}")
            .AsNoTracking().ToListAsync(ct)).SingleOrDefault();

    private async Task<UserSession?> LockSessionAsync(SessionReference ticket, CancellationToken ct)
        => (await db.UserSessions.FromSqlInterpolated(
            $"SELECT * FROM dbo.user_sessions WITH (UPDLOCK, HOLDLOCK) WHERE user_id = {ticket.UserId} AND session_key_hash = {ticket.KeyHash}")
            .AsNoTracking().ToListAsync(ct)).SingleOrDefault();

    private async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("A caller-owned SQL session transaction is required.");
        if (!db.Database.IsSqlServer() || tx.GetDbTransaction().IsolationLevel
            is not (IsolationLevel.ReadCommitted or IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new InvalidOperationException("SQL Server locking isolation is required.");
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Session operations require a clean untracked context.");
        try { return await work(); }
        catch (Exception e) when (!ct.IsCancellationRequested && SessionStoreUnavailableException.Matches(e))
        { throw new SessionStoreUnavailableException(e); }
    }

    private static AuditLog Audit(long userId, string authenticationEvent, DateTime now)
        => AuthenticationAudit.ForUser(userId, authenticationEvent == "SESSION_REVOKE" ? "LOGOUT" : "UPDATE",
            now, new { authenticationEvent });

    private async Task WriteAuditAsync(long userId, string action, DateTime now, CancellationToken ct)
    {
        var audit = Audit(userId, action, now); db.AuditLogs.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(audit).State = EntityState.Detached; }
    }
}
