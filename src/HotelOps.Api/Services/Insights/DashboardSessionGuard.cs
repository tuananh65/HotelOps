using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;

namespace HotelOps.Api.Services.Insights;

public sealed class DashboardSessionGuard(IHttpContextAccessor http, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal async Task RequireAsync(CancellationToken ct)
    {
        var context = http.HttpContext;
        if (context == null || !context.Request.IsHttps
            || !HotelOpsCookieTicket.TryRead(context.User, out var ticket, out var sid)
            || !current.TryGetCurrentUser(out var actor) || actor.SessionId != sid || actor.UserId != ticket!.UserId
            || actor.IsRestricted || ticket.IsRestricted)
            throw new InsightException(401, "SESSION_INVALID", "Current unrestricted HTTPS session required.");
        var result = await sessions.ValidateSessionAsync(ticket, ct);
        if (!result.IsValid || result.Session?.SessionId != sid || result.Session.IsRestricted)
            throw new InsightException(401, "SESSION_INVALID", "Session expired or revoked.");
    }
}
