using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;

namespace HotelOps.Api.Security;

/// <summary>Bind tokens to the session as well as the framework's user identity.</summary>
public sealed class SessionAntiforgeryAdditionalDataProvider : IAntiforgeryAdditionalDataProvider
{
    public string GetAdditionalData(HttpContext context)
        => GetBinding(context) ?? throw new InvalidOperationException("Unsupported authentication identity for antiforgery.");

    public bool ValidateAdditionalData(HttpContext context, string additionalData)
    {
        var expected = GetBinding(context);
        return expected is not null && string.Equals(expected, additionalData, StringComparison.Ordinal);
    }

    private static string? GetBinding(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true) return "v1:anonymous";
        if (!HotelOpsCookieTicket.TryRead(context.User, out var reference, out var sessionId)) return null;
        // Nonsecret IDs only, protected inside the antiforgery token. No raw key/hash.
        return string.Create(CultureInfo.InvariantCulture,
            $"v1:{reference!.UserId}:{sessionId}:{reference.CreatedAtUtc.Ticks}");
    }
}
