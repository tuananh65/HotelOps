using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.DTOs.Authentication;

// Linked source shared only by the four DEV2 HTTP runners.
internal sealed class InventoryAuthenticatedClient : HttpClient
{
    private readonly CsrfHandler csrf;
    private readonly LoginRequest credentials;

    public long UserId { get; private set; }
    public InventoryAuthenticatedClient(string prefix) : this(new CsrfHandler(), prefix) { }

    private InventoryAuthenticatedClient(CsrfHandler handler, string prefix) : base(handler)
    {
        csrf = handler;
        credentials = new LoginRequest
        {
            Username = RequiredEnvironment(prefix + "_USERNAME"),
            Password = RequiredEnvironment(prefix + "_PASSWORD")
        };
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        using var bootstrap = await GetAsync("/api/auth/csrf", cancellationToken);
        RequireSuccess(bootstrap, "CSRF bootstrap");
        var data = await bootstrap.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var header = RequiredString(data, "headerName");
        if (header != "X-CSRF-TOKEN")
            throw new InvalidOperationException("Unexpected CSRF header name; response redacted.");
        csrf.HeaderName = header;
        csrf.Token = RequiredString(data, "csrfToken");

        using var login = await this.PostAsJsonAsync("/api/auth/login", credentials, cancellationToken);
        RequireSuccess(login, "Login");
        var session = await login.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        csrf.Token = RequiredString(session, "csrfToken");
        UserId = long.Parse(RequiredString(session.GetProperty("user"), "id"), System.Globalization.CultureInfo.InvariantCulture);
        if (session.GetProperty("mustChangePassword").GetBoolean())
            throw new InvalidOperationException("Test account requires a password change; prepare an unrestricted credential externally.");
    }

    public async Task LogoutAsync()
    {
        if (UserId == 0) return;
        using var response = await this.PostAsJsonAsync("/api/auth/logout", new { });
        RequireSuccess(response, "Logout");
        UserId = 0;
    }
    private static string RequiredEnvironment(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(value) ? value
            : throw new InvalidOperationException($"Supply {name} through the process environment; credential values are never printed.");
    }

    private static string RequiredString(JsonElement data, string property)
    {
        if (data.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())) return value.GetString()!;
        throw new InvalidOperationException($"Authentication response missing {property}; response redacted.");
    }

    private static void RequireSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{operation} failed: HTTP {(int)response.StatusCode}; response redacted.");
    }

    private sealed class CsrfHandler : DelegatingHandler
    {
        public string? HeaderName { get; set; }
        public string? Token { get; set; }

        public CsrfHandler() : base(new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = false
        }) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Patch || request.Method == HttpMethod.Delete)
            {
                if (HeaderName is null || Token is null)
                    throw new InvalidOperationException("CSRF bootstrap and login are required before business writes.");
                request.Headers.Remove(HeaderName);
                request.Headers.Add(HeaderName, Token);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
