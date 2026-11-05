using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Authentication;

public enum ReauthenticationStatus { Succeeded, SessionInvalid, Restricted, InvalidCredential, Locked, ProofRequired }
public sealed record ReauthenticationResult(ReauthenticationStatus Status, DateTime? ExpiresAtUtc = null);

public sealed class AdminReauthenticationService(HotelOpsDbContext db, ISqlSessionService sessions,
    ICredentialLockoutService lockout, ReauthenticationProofStore proofs, TimeProvider clock)
{
    public async Task<ReauthenticationResult> ReauthenticateAsync(SessionReference reference, long sessionId,
        string currentPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || currentPassword.Length > 256)
            throw new ArgumentException("Invalid current password input.", nameof(currentPassword));
        if (db.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null
            || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Reauthentication requires its own clean transaction.");
        proofs.Forget(reference, sessionId);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var validation = await sessions.ValidateSessionAsync(reference, ct);
        if (!validation.IsValid || validation.Session is not { } session || session.SessionId != sessionId)
            return new(ReauthenticationStatus.SessionInvalid);
        if (session.IsRestricted) return new(ReauthenticationStatus.Restricted);
        var verified = await lockout.VerifyAsync(reference.UserId, currentPassword,
            (user, _) => Task.FromResult(SqlSessionPolicy.IsUserActive(user) && user.TemporaryPasswordExpiresAt == null), ct);
        if (verified.Status != CredentialLockoutStatus.Verified)
        {
            await tx.CommitAsync(ct); // Persist existing brute-force counters/lockout, never revoke/replace session.
            return new(verified.Status == CredentialLockoutStatus.Locked ? ReauthenticationStatus.Locked : ReauthenticationStatus.InvalidCredential);
        }
        // Session may cross an expiry while hashing. User-first locks prevent rotation/revoke overtaking verification.
        validation = await sessions.ValidateSessionAsync(reference, ct);
        if (!validation.IsValid || validation.Session is not { } latest || latest.SessionId != sessionId)
            return new(ReauthenticationStatus.SessionInvalid);
        var now = clock.GetUtcNow().UtcDateTime;
        var expiry = new[] { now.Add(ReauthenticationProofStore.MaximumAge), latest.ExpiresAtUtc, latest.AbsoluteExpiresAtUtc }.Min();
        var version = await db.Users.AsNoTracking().Where(u => u.UserId == reference.UserId).Select(u => u.RowVersion).SingleAsync(ct);
        var audit = AuthenticationAudit.ForUser(reference.UserId, "UPDATE", now,
            new { authenticationEvent = "REAUTHENTICATED", session_id = sessionId, expires_at_utc = expiry });
        audit.ActorUserId = reference.UserId;
        db.AuditLogs.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(audit).State = EntityState.Detached; }
        await tx.CommitAsync(ct);
        proofs.Publish(reference, sessionId, version, now, expiry);
        return new(ReauthenticationStatus.Succeeded, expiry);
    }

    /// <summary>Must run inside the sensitive operation's user-first SQL transaction, never a preflight-only check.</summary>
    public async Task<ReauthenticationStatus> ValidateProofAsync(SessionReference reference, long sessionId, CancellationToken ct)
    {
        var valid = await sessions.ValidateSessionAsync(reference, ct);
        if (!valid.IsValid || valid.Session is not { } session || session.SessionId != sessionId)
        {
            proofs.Forget(reference, sessionId); return ReauthenticationStatus.SessionInvalid;
        }
        if (session.IsRestricted) { proofs.Forget(reference, sessionId); return ReauthenticationStatus.Restricted; }
        var version = await db.Users.AsNoTracking().Where(u => u.UserId == reference.UserId).Select(u => u.RowVersion).SingleAsync(ct);
        return proofs.Matches(reference, sessionId, version) ? ReauthenticationStatus.Succeeded : ReauthenticationStatus.ProofRequired;
    }
}
