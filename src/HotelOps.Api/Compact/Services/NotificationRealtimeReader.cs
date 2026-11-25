using System.Security.Claims;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed record NotificationPulse(string EventId, int UnreadCount, long[] NotificationIds);

// Fresh scopes read committed notifications; no event can escape a rolled-back writer.
public sealed class NotificationRealtimeReader(IServiceScopeFactory scopes, TimeProvider clock)
{
    public async Task<NotificationPulse?> Read(ClaimsPrincipal principal, long? branchId, CancellationToken ct)
    {
        if (!long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || !long.TryParse(principal.FindFirstValue("session_id"), out var sessionId)) return null;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var active = await (from session in db.UserSessions.AsNoTracking()
            join user in db.Users.AsNoTracking() on session.UserId equals user.UserId
            where session.SessionId == sessionId && session.UserId == userId
                && session.RevokedAt == null && session.ExpiresAt > now
                && session.SecurityStamp == user.SecurityStamp
                && user.TemporaryPasswordExpiresAt == null && user.AccountStatus == "ACTIVE"
                && user.EmploymentStatus == "ACTIVE" && user.DeletedAt == null
            select session.SessionId).AnyAsync(ct);
        if (!active) return null;

        // A fixed accessor is local to this read, not the global HTTP AsyncLocal accessor.
        var actor = new Actor(new PrincipalAccessor { HttpContext = new DefaultHttpContext { User = principal } });
        var access = new Access(db, actor, clock);
        var notifications = new NotificationService(db, actor, access, new Atomic(db), clock);
        return await notifications.RealtimeSnapshot(branchId, ct);
    }

    private sealed class PrincipalAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
