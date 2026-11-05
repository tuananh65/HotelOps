using Microsoft.AspNetCore.Antiforgery;

namespace HotelOps.Api.Security;

public static class HotelOpsAntiforgery
{
    public const string HeaderName = "X-CSRF-TOKEN";
    public const string CookieName = "__Host-HotelOps.Antiforgery";

    public static IServiceCollection AddHotelOpsAntiforgery(this IServiceCollection services)
    {
        services.AddSingleton<IAntiforgeryAdditionalDataProvider, SessionAntiforgeryAdditionalDataProvider>();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = CookieName;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.Cookie.Domain = null;
        });
        return services;
    }

    /// <summary>
    /// After Login/ChangePassword the caller MUST replace HttpContext.User with
    /// the new principal before issuing tokens. SignInAsync alone does not do so.
    /// Do not reuse tokens belonging to the previous/anonymous identity.
    /// </summary>
    public static CsrfBootstrapResponse Issue(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Headers.CacheControl = "no-store";
        return new(tokens.RequestToken ?? throw new InvalidOperationException("Antiforgery request token unavailable."), HeaderName);
    }
}

public sealed class CsrfBootstrapResponse(string csrfToken, string headerName)
{
    public string CsrfToken { get; } = csrfToken;
    public string HeaderName { get; } = headerName;
    public override string ToString() => "[CSRF bootstrap response: token redacted]";
}
