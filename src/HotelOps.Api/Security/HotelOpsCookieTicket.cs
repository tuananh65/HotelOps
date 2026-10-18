using System.Globalization;
using System.Security.Claims;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Authentication;

namespace HotelOps.Api.Security;

public static class HotelOpsCookieTicket
{
    public const string Scheme = "HotelOpsCookie";
    public const string CookieName = "__Host-HotelOps.Auth";
    public const string SessionIdClaim = "hotelops:session_id";
    public const string CredentialClaim = "hotelops:session_credential";
    public const string RestrictedClaim = "hotelops:restricted";
    public const string CreatedClaim = "hotelops:created_ticks";
    public const string VersionClaim = "hotelops:ticket_version";

    /// <summary>Call only after SQL commit; never serialize/log this ticket or its claims.</summary>
    public static AuthenticationTicket Create(CreatedSqlSession created, TimeProvider clock)
    {
        var s = created.Session;
        var reference = created.Ticket;
        var policyCap = s.CreatedAtUtc.Add(s.IsRestricted ? SqlSessionPolicy.RestrictedLifetime : SqlSessionPolicy.NormalAbsoluteLifetime);
        if (s.SessionId <= 0 || s.UserId != reference.UserId || s.CreatedAtUtc.Kind != DateTimeKind.Utc
            || s.CreatedAtUtc != reference.CreatedAtUtc || s.IsRestricted != reference.IsRestricted
            || s.AbsoluteExpiresAtUtc > policyCap || s.AbsoluteExpiresAtUtc <= clock.GetUtcNow().UtcDateTime
            || s.ExpiresAtUtc > s.AbsoluteExpiresAtUtc || s.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException("Cannot issue a ticket for an invalid session window.");

        var principal = BuildPrincipal(reference, s.SessionId);
        return new(principal, new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            IssuedUtc = new DateTimeOffset(s.CreatedAtUtc),
            ExpiresUtc = new DateTimeOffset(s.AbsoluteExpiresAtUtc)
        }, Scheme);
    }

    internal static ClaimsPrincipal BuildPrincipal(SessionReference ticket, long sessionId) => new(new ClaimsIdentity(
    [
        new(ClaimTypes.NameIdentifier, ticket.UserId.ToString(CultureInfo.InvariantCulture)),
        new(SessionIdClaim, sessionId.ToString(CultureInfo.InvariantCulture)),
        new(CredentialClaim, ticket.ExportCredentialForProtectedTicket()),
        new(RestrictedClaim, ticket.IsRestricted ? "1" : "0"),
        new(CreatedClaim, ticket.CreatedAtUtc.Ticks.ToString(CultureInfo.InvariantCulture)),
        new(VersionClaim, "1")
    ], Scheme));

    internal static bool TryRead(ClaimsPrincipal? principal, out SessionReference? reference, out long sessionId)
    {
        reference = null; sessionId = 0;
        if (principal is null || principal.Identities.Count() != 1
            || principal.Identity?.AuthenticationType != Scheme || principal.Identity.IsAuthenticated != true)
            return false;
        string? One(string type)
        {
            var values = principal.FindAll(type).ToArray();
            return values.Length == 1 ? values[0].Value : null;
        }
        var raw = One(CredentialClaim);
        var restricted = One(RestrictedClaim);
        if (One(VersionClaim) != "1" || restricted is not ("0" or "1")
            || !long.TryParse(One(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0
            || !long.TryParse(One(SessionIdClaim), NumberStyles.None, CultureInfo.InvariantCulture, out sessionId) || sessionId <= 0
            || !long.TryParse(One(CreatedClaim), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks || !SessionCredential.IsValid(raw))
            return false;
        reference = SessionReference.FromProtectedTicket(userId, raw!, restricted == "1", new DateTime(ticks, DateTimeKind.Utc));
        return true;
    }
}
