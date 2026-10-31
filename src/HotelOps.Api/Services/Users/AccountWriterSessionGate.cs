using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

// Revalidates an already authenticated server-side identity, never authenticates caller input.
// Create/Profile already retain the Users universe before invoking this gate. Taking a
// session lock here cannot precede or upgrade the canonical User locks.
internal static class AccountWriterSessionGate
{
    internal static async Task<bool> IsValidAsync(HotelOpsDbContext db, CurrentUserInfo actor,
        TimeProvider clock, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Account writer session validation requires the retained User transaction.");
        if (actor.IsRestricted || actor.SessionId <= 0) return false;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == actor.UserId, ct);
        if (user is null || !SqlSessionPolicy.IsUserActive(user) || user.TemporaryPasswordExpiresAt.HasValue) return false;
        var session = await db.UserSessions.FromSqlInterpolated(
            $"SELECT * FROM dbo.user_sessions WITH (UPDLOCK,HOLDLOCK) WHERE session_id={actor.SessionId} AND user_id={actor.UserId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (session is null) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        var absolute = SqlSessionPolicy.AbsoluteDeadline(session.CreatedAt, user.TemporaryPasswordExpiresAt);
        // The protected ticket was authenticated by CurrentUserAccessor; bind its session id
        // back to SQL and recheck policy at the mutation boundary, including expiry after waits.
        return session.RevokedAt is null && session.CreatedAt <= now && now < session.ExpiresAt
            && now < absolute && session.ExpiresAt <= absolute && session.ExpiresAt > session.CreatedAt
            && (session.LastUsedAt is null || session.LastUsedAt >= session.CreatedAt && session.LastUsedAt <= session.ExpiresAt);
    }
}
