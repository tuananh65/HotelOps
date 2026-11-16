using System.Linq.Expressions;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;

namespace HotelOps.Api.Services.Notifications;

internal static class NotificationSessionPolicy
{
    internal static Expression<Func<UserSession, bool>> Current(long user, long session, DateTime now)
    {
        var hours = SqlSessionPolicy.NormalAbsoluteLifetime.TotalHours;
        return s => s.SessionId == session && s.UserId == user && s.RevokedAt == null && s.ExpiresAt > now
            && s.CreatedAt <= now && s.CreatedAt.AddHours(hours) > now && s.ExpiresAt <= s.CreatedAt.AddHours(hours)
            && s.User.DeletedAt == null && s.User.AccountStatus == "ACTIVE" && s.User.TemporaryPasswordExpiresAt == null;
    }
}
