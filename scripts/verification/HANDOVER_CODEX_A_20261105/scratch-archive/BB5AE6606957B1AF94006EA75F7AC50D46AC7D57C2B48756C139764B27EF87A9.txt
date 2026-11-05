using System.Data;
using System.Globalization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Authentication;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Authentication;

public sealed class AuthenticationLoginService(HotelOpsDbContext db, ICredentialLockoutService lockout,
    ISqlSessionService sessions, IPasswordCredentialService passwords, TimeProvider clock) : IAuthenticationLoginService
{
    public Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        => RunAsync<LoginResult>(async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var identifier = request.Username?.Trim();
            if (string.IsNullOrEmpty(identifier) || identifier.Length > 150
                || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 256)
                throw new ArgumentException("Invalid login input.");

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            // Individual UNIQUE indexes do not prevent username/email cross-collisions.
            // Use database collation, not application case folding or email heuristics.
            var candidates = await Candidates(identifier).ToListAsync(cancellationToken);
            User? verifiedUser = null;
            var temporaryExpired = false;
            var result = await lockout.VerifyAsync(candidates.Count == 1 ? candidates[0] : null,
                request.Password, async (user, ct) =>
                {
                    // Called only after a supported password has been verified, under user lock.
                    var latestCandidates = await Candidates(identifier).ToListAsync(ct);
                    if (latestCandidates.Count != 1 || latestCandidates[0] != user.UserId
                        || !SqlSessionPolicy.IsUserActive(user)) return false;
                    verifiedUser = user;
                    temporaryExpired = user.TemporaryPasswordExpiresAt <= clock.GetUtcNow().UtcDateTime;
                    return !temporaryExpired;
                }, cancellationToken);

            // A deadline can cross during verification; never clear its non-NULL marker.
            temporaryExpired = verifiedUser?.TemporaryPasswordExpiresAt <= clock.GetUtcNow().UtcDateTime;
            if (result.Status != CredentialLockoutStatus.Verified)
            {
                await transaction.CommitAsync(cancellationToken); // Persist ordinary failure counters/lockout audit.
                return result.Status == CredentialLockoutStatus.Locked
                    ? new(LoginStatus.Locked, lockedUntil: Utc(result.LockedUntil))
                    : new(temporaryExpired ? LoginStatus.TemporaryExpired : LoginStatus.InvalidCredentials);
            }
            if (temporaryExpired)
                return new(LoginStatus.TemporaryExpired); // Dispose rolls back provisional counter reset.

            var user = verifiedUser ?? throw new InvalidOperationException("Verified user is missing.");
            // Consumes proof and rechecks hash/expiry under the SAME user-first transaction.
            var created = await sessions.CreateSessionAsync(result, cancellationToken);
            var now = clock.GetUtcNow().UtcDateTime;
            var update = db.Users.Where(u => u.UserId == user.UserId && u.PasswordHash == user.PasswordHash
                && u.TemporaryPasswordExpiresAt == user.TemporaryPasswordExpiresAt);
            var replacementHash = result.RehashNeeded ? passwords.HashPassword(user, request.Password) : null;
            // Rehash only after proof consumption. Never clear temporary expiry or overwrite unrelated fields.
            var changed = result.RehashNeeded
                ? await update.ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, now)
                    .SetProperty(u => u.PasswordHash, replacementHash), cancellationToken)
                : await update.ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, now), cancellationToken);
            if (changed != 1) throw new DbUpdateConcurrencyException("Credential state changed during login.");
            var audit = AuthenticationAudit.ForUser(user.UserId, "LOGIN", now, new { authenticationEvent = "LOGIN_SUCCESS" });
            audit.ActorUserId = user.UserId;
            db.AuditLogs.Add(audit);
            try { await db.SaveChangesAsync(cancellationToken); }
            finally { db.Entry(audit).State = EntityState.Detached; }
            await transaction.CommitAsync(cancellationToken);
            return new(LoginStatus.Succeeded, Dto(user), created);
        }, cancellationToken);

    public Task<CurrentSession?> GetSessionAsync(SessionReference reference, long sessionId,
        CancellationToken cancellationToken = default)
        => RunAsync<CurrentSession?>(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            // Revalidate within the read transaction so a reset after middleware cannot expose stale data.
            var valid = await sessions.ValidateSessionAsync(reference, cancellationToken);
            if (!valid.IsValid || valid.Session is not { } snapshot || snapshot.SessionId != sessionId
                || snapshot.UserId != reference.UserId) return null;
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == reference.UserId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(Dto(user), snapshot);
        }, cancellationToken);

    private IQueryable<long> Candidates(string identifier) => db.Users.AsNoTracking()
        .Where(u => u.Username == identifier || u.Email == identifier).Select(u => u.UserId).Take(2);

    private async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null
            || System.Transactions.Transaction.Current != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Authentication orchestration requires a clean SQL Server context without an existing transaction.");
        try { return await work(); }
        catch (Exception e) when (!ct.IsCancellationRequested && e is not SessionStoreUnavailableException
            && SessionStoreUnavailableException.Matches(e)) { throw new SessionStoreUnavailableException(e); }
    }

    private static AuthUserDto Dto(User user) => new(user.UserId.ToString(CultureInfo.InvariantCulture), user.Username, user.FullName);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
