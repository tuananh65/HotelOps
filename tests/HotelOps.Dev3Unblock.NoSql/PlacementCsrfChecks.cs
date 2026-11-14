using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

internal static class PlacementCsrfChecks
{
    internal static async Task<IReadOnlyList<string>> RunAsync()
    {
        var checks = new List<string>();
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        foreach (var path in new[] { "/api/assets/1/location", "/api/assets/1/location-transfers", "/api/incidents" })
        foreach (var mode in new[] { "missing", "invalid", "valid" })
        {
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Method = "POST";
            context.Request.Path = path;
            context.Response.Body = new MemoryStream();
            if (mode != "missing") context.Request.Headers[HotelOpsAntiforgery.HeaderName] = "synthetic-only";
            var called = false;
            var csrf = new Antiforgery(mode == "valid");
            await new ApiCsrfMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, csrf);
            if (called != (mode == "valid") || csrf.Calls != (mode == "missing" ? 0 : 1)
                || mode != "valid" && context.Response.StatusCode != 400)
                throw new Exception($"Global CSRF middleware regression: {path} {mode}");
            checks.Add($"Global API CSRF {mode}: {path}");
        }
        return checks;
    }

    private sealed class Antiforgery(bool valid) : IAntiforgery
    {
        internal int Calls { get; private set; }
        public Task ValidateRequestAsync(HttpContext context)
        {
            Calls++;
            return valid ? Task.CompletedTask : throw new AntiforgeryValidationException("Synthetic rejection");
        }
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext context) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext context) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext context) => throw new NotSupportedException();
        public void SetCookieTokenAndHeader(HttpContext context) => throw new NotSupportedException();
    }
}
