namespace HotelOps.Api.Security;

public static class CookieRequestRules
{
    public static bool IsApi(HttpRequest request) => request.Path.StartsWithSegments("/api");

    public static bool IsCsrfBootstrap(HttpRequest request)
        => HttpMethods.IsGet(request.Method) && IsPath(request, "/api/auth/csrf");

    public static bool IsLogin(HttpRequest request)
        => HttpMethods.IsPost(request.Method) && IsPath(request, "/api/auth/login");

    public static bool IsLogout(HttpRequest request)
        => HttpMethods.IsPost(request.Method) && IsPath(request, "/api/auth/logout");

    public static bool IsSafeMethod(HttpRequest request)
        => HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);

    public static bool IsRestrictedAllowed(HttpRequest request)
        => (HttpMethods.IsGet(request.Method)
                && (IsPath(request, "/api/auth/session") || IsPath(request, "/api/auth/csrf")))
            || (HttpMethods.IsPost(request.Method)
                && (IsPath(request, "/api/auth/change-password") || IsPath(request, "/api/auth/logout")));

    private static bool IsPath(HttpRequest request, string path)
        => string.Equals(request.Path.Value?.TrimEnd('/'), path, StringComparison.OrdinalIgnoreCase);
}
