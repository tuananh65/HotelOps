using HotelOps.Api.Services.Authentication;

namespace HotelOps.Api.Security.Authorization;

/// <summary>Reads request-local authenticated identity; does not grant permissions.</summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user)
    {
        user = default;
        var context = httpContextAccessor.HttpContext;
        if (context is null
            || !HotelOpsCookieTicket.TryRead(context.User, out var reference, out var sessionId)
            || reference is null
            || context.Items[typeof(SessionSnapshot)] is not SessionSnapshot session
            || session.UserId != reference.UserId
            || session.SessionId != sessionId
            || session.IsRestricted != reference.IsRestricted
            || session.CreatedAtUtc != reference.CreatedAtUtc)
            return false;

        // Snapshot is supplied by cookie validation in this request; no SQL revalidation.
        user = new(session.UserId, session.SessionId, session.IsRestricted);
        return true;
    }
}
