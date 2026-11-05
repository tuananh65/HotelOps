using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Authentication;

public static class SqlSessionPolicy
{
    public static readonly TimeSpan NormalIdleTimeout = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan NormalAbsoluteLifetime = TimeSpan.FromHours(12);
    public static readonly TimeSpan RestrictedLifetime = TimeSpan.FromMinutes(15);

    public static bool IsUserActive(User user) => user.DeletedAt is null && user.AccountStatus == "ACTIVE";

    public static DateTime AbsoluteDeadline(DateTime created, DateTime? temporaryExpiry)
    {
        var deadline = created.Add(temporaryExpiry.HasValue ? RestrictedLifetime : NormalAbsoluteLifetime);
        return temporaryExpiry.HasValue && temporaryExpiry.Value < deadline ? temporaryExpiry.Value : deadline;
    }

    public static SessionSnapshot? Validate(User user, UserSession session, SessionReference ticket, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC time is required.");
        var restricted = user.TemporaryPasswordExpiresAt.HasValue;
        var absolute = AbsoluteDeadline(session.CreatedAt, user.TemporaryPasswordExpiresAt);
        if (!IsUserActive(user) || session.UserId != user.UserId || ticket.UserId != user.UserId
            || ticket.IsRestricted != restricted || ticket.CreatedAtUtc != session.CreatedAt
            || session.SessionKeyHash != ticket.KeyHash || session.RevokedAt.HasValue
            || utcNow < session.CreatedAt || utcNow >= session.ExpiresAt || utcNow >= absolute
            || session.ExpiresAt > absolute || session.ExpiresAt <= session.CreatedAt
            || session.LastUsedAt < session.CreatedAt || session.LastUsedAt > session.ExpiresAt)
            return null;

        return new(session.SessionId, user.UserId, DateTime.SpecifyKind(session.CreatedAt, DateTimeKind.Utc),
            DateTime.SpecifyKind(session.ExpiresAt, DateTimeKind.Utc), DateTime.SpecifyKind(absolute, DateTimeKind.Utc), restricted);
    }

    public static (DateTime LastUsedAt, DateTime ExpiresAt) TouchWindow(UserSession session, SessionSnapshot valid, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc || utcNow >= valid.ExpiresAtUtc || utcNow >= valid.AbsoluteExpiresAtUtc
            || session.RevokedAt.HasValue)
            throw new InvalidOperationException("Cannot touch an invalid session.");
        var activity = session.LastUsedAt > utcNow ? session.LastUsedAt.Value : utcNow;
        var deadline = activity.Add(valid.IsRestricted ? RestrictedLifetime : NormalIdleTimeout);
        if (deadline > valid.AbsoluteExpiresAtUtc) deadline = valid.AbsoluteExpiresAtUtc;
        if (deadline < session.ExpiresAt) deadline = session.ExpiresAt;
        return (DateTime.SpecifyKind(activity, DateTimeKind.Utc), DateTime.SpecifyKind(deadline, DateTimeKind.Utc));
    }
}
