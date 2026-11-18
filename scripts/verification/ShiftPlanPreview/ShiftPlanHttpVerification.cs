using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Isolated loopback host: real MVC controller and CSRF middleware, simulated identity/session.
// Optional live SQL only reads scope and previews; never saves assignments or creates sessions.
internal static class ShiftPlanHttpVerification
{
    public static async Task RunAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ct = timeout.Token;
        var connection = Environment.GetEnvironmentVariable("HOTELOPS_SHIFT_VERIFY_CONNECTION");
        long actorId = 7, employeeId = 6, branchId = 1, departmentId = 1, templateId = 1;
        var existingOverlap = false;
        var freeDate = new DateOnly(2026, 11, 20);
        if (connection is not null)
        {
            await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
            var actor = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "ha.pham", ct);
            var employee = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "lan.dang", ct);
            actorId = actor.UserId; employeeId = employee.UserId;
            branchId = actor.PrimaryBranchId ?? throw new Exception("Missing branch");
            departmentId = employee.DepartmentId ?? throw new Exception("Missing department");
            templateId = await db.ShiftTemplates.AsNoTracking().Where(t => t.BranchId == branchId && t.IsActive
                && t.StartTime == new TimeOnly(6, 0) && t.EndTime == new TimeOnly(14, 0))
                .Select(t => t.ShiftTemplateId).FirstAsync(ct);
            var start = new DateTime(2026, 11, 18, 23, 0, 0, DateTimeKind.Utc);
            var end = start.AddHours(8);
            var existing = await db.ShiftAssignments.AsNoTracking().Where(a => a.UserId == employeeId
                && a.AssignmentStatus != "CANCELLED" && a.Shift.ScheduledStartAt < end && a.Shift.ScheduledEndAt > start)
                .Select(a => new { a.ShiftId, a.AssignmentStatus, a.Shift.ScheduledStartAt, a.Shift.ScheduledEndAt }).ToArrayAsync(ct);
            existingOverlap = existing.Length > 0;
            foreach (var shift in existing) Console.WriteLine($"Existing assignment: shift={shift.ShiftId}; {shift.AssignmentStatus}; UTC {shift.ScheduledStartAt:O} - {shift.ScheduledEndAt:O}");
            for (var attempt = 0; attempt < 30; attempt++, freeDate = freeDate.AddDays(1))
            {
                var freeStart = freeDate.ToDateTime(new TimeOnly(6, 0)).AddHours(-7);
                var freeEnd = freeStart.AddHours(8);
                if (!await db.ShiftAssignments.AsNoTracking().AnyAsync(a => a.UserId == employeeId && a.AssignmentStatus != "CANCELLED"
                    && a.Shift.ScheduledStartAt < freeEnd && a.Shift.ScheduledEndAt > freeStart, ct)) break;
            }
        }
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.Services.AddHotelOpsAntiforgery();
        builder.Services.AddSingleton(new Identity(actorId));
        builder.Services.AddAuthentication(HotelOpsCookieTicket.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(HotelOpsCookieTicket.Scheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ICurrentUserAccessor>(new Actor(actorId, connection is not null));
        builder.Services.AddSingleton<ISqlSessionService>(new Sessions(actorId));
        builder.Services.AddDbContext<HotelOpsDbContext>(o => o.UseSqlServer(connection
            ?? "Server=offline.invalid;Database=NeverConnect;Integrated Security=True;Connect Timeout=1;Encrypt=True"));
        builder.Services.AddScoped<IPermissionDataSource, EfPermissionDataSource>();
        builder.Services.AddScoped<SqlSecureShiftPreview>();
        builder.Services.AddScoped<ShiftPlanSaveService>();
        // Match production's AddControllers, deliberately without MVC ViewFeatures.
        builder.Services.AddControllers().AddApplicationPart(typeof(ShiftPlanPreviewController).Assembly)
            .ConfigureApplicationPartManager(m => m.FeatureProviders.Add(new OnlyShiftController()));
        await using var app = builder.Build();
        // No production cookies or credentials are used. Mark the isolated requests HTTPS
        // for secure antiforgery issuance and the controller's HTTPS guard.
        app.Use((context, next) => { context.Request.Scheme = "https"; return next(context); });
        app.UseRouting(); app.UseAuthentication();
        app.UseMiddleware<ApiCsrfMiddleware>();
        app.UseAuthorization();
        app.MapGet("/api/test/csrf", (HttpContext c, IAntiforgery a) => HotelOpsAntiforgery.Issue(c, a));
        app.MapControllers();
        await app.StartAsync(ct);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        var checks = 0;
        void Check(bool success, string label) { if (!success) throw new Exception("FAIL " + label); checks++; Console.WriteLine("PASS " + label); }
        async Task<(string Cookie, string Token)> Bootstrap(bool authenticated)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test/csrf");
            if (authenticated) request.Headers.Add("X-Test-Identity", "fixture");
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return (response.Headers.GetValues("Set-Cookie").Single().Split(';')[0], json.GetProperty("csrfToken").GetString()!);
        }
        async Task<(int Status, JsonElement Body)> Post(string path, object body, (string Cookie, string Token) tokens,
            bool authenticated = true, string tokenMode = "valid")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            if (authenticated) request.Headers.Add("X-Test-Identity", "fixture");
            request.Headers.Add("Cookie", tokens.Cookie);
            if (tokenMode != "missing") request.Headers.Add(HotelOpsAntiforgery.HeaderName, tokenMode == "invalid" ? "invalid" : tokens.Token);
            using var response = await client.SendAsync(request, ct);
            var bodyText = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, bodyText.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(bodyText));
        }
        try
        {
            var tokens = await Bootstrap(true);
            var route = $"/api/branches/{branchId}/departments/{departmentId}/shift-plans";
            var input = new { drafts = new[] { new { employeeId, templateId, shiftDate = "2026-11-19" } } };
            foreach (var endpoint in new[] { route + "/preview", route })
            {
                foreach (var mode in new[] { "missing", "invalid" })
                {
                    var response = await Post(endpoint, input, tokens, tokenMode: mode);
                    Check(response.Status == 400 && response.Body.GetProperty("code").GetString() == "CSRF_VALIDATION_FAILED", endpoint + " rejects " + mode + " CSRF");
                }
            }
            var anonymousTokens = await Bootstrap(false);
            Check((await Post(route + "/preview", input, anonymousTokens, false)).Status == 401, "anonymous preview rejected");
            Check((await Post(route, input, anonymousTokens, false)).Status == 401, "anonymous save rejected");
            Check((await Post(route + "/preview", input, anonymousTokens)).Status == 400, "CSRF token from different identity rejected");
            Check((await Post(route + "/preview", new { drafts = input.drafts, role = "admin" }, tokens)).Status == 400, "unknown JSON fields rejected");
            var preview = await Post(route + "/preview", input, tokens);
            Check(preview.Status == (connection is null ? 401 : 200), "valid CSRF reaches preview controller without ViewFeatures failure");
            // Empty input cannot write; with simulated SQL admission it reaches service input validation.
            Check((await Post(route, new { drafts = Array.Empty<object>() }, tokens)).Status == (connection is null ? 401 : 400), "valid CSRF reaches save controller without writing");
            if (connection is not null)
            {
                var row = preview.Body.GetProperty("rows")[0];
                Console.WriteLine("Preview diagnostics: " + row.GetRawText());
                Check(row.GetProperty("issues").EnumerateArray().Select(i => i.GetInt32()).SequenceEqual(existingOverlap ? new[] { 7 } : Array.Empty<int>()),
                    "Ha previews Lan morning shift 19/11/2026; conflict result matches stored assignments");
                Check(row.GetProperty("startUtc").GetDateTimeOffset() == new DateTimeOffset(2026, 11, 18, 23, 0, 0, TimeSpan.Zero), "06:00 Vietnam correctly converted to UTC");
                Check((await Post("/api/branches/9223372036854775807/departments/1/shift-plans/preview", input, tokens)).Status == 403, "outside branch scope denied");
                var free = await Post(route + "/preview", new { drafts = new[] { new { employeeId, templateId, shiftDate = freeDate.ToString("yyyy-MM-dd") } } }, tokens);
                Check(free.Status == 200 && free.Body.GetProperty("rows")[0].GetProperty("issues").GetArrayLength() == 0,
                    "free morning shift " + freeDate + " previews without conflicts");
            }
            Console.WriteLine($"RESULT: {checks} HTTP checks PASS; SQL={(connection is null ? "NOT_RUN" : "read-only real data")}; identity/session simulated; saving NOT_RUN.");
        }
        finally { await app.StopAsync(CancellationToken.None); }
    }
    private sealed record Identity(long UserId)
    {
        public DateTime Created { get; } = DateTime.UtcNow;
        public ClaimsPrincipal Principal => HotelOpsCookieTicket.Create(new(
            SessionReference.FromProtectedTicket(UserId, new string('A', 43), false, Created),
            new SessionSnapshot(1, UserId, Created, Created.AddMinutes(30), Created.AddHours(1), false)), TimeProvider.System).Principal;
    }
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, Identity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            Request.Headers.ContainsKey("X-Test-Identity") ? AuthenticateResult.Success(new AuthenticationTicket(identity.Principal, Scheme.Name)) : AuthenticateResult.NoResult());
    }
    private sealed class Actor(long id, bool valid) : ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) { user = new(id, 1, false); return valid; }
    }
    private sealed class Sessions(long id) : ISqlSessionService
    {
        public Task<SessionValidationResult> ValidateSessionAsync(SessionReference t, CancellationToken ct = default) => Task.FromResult(
            new SessionValidationResult(true, new SessionSnapshot(1, id, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow.AddHours(1), false)));
        public Task<CreatedSqlSession> CreateSessionAsync(CredentialLockoutResult c, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SessionValidationResult> TouchSessionAsync(SessionReference t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RevokeSessionAsync(SessionReference t, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> RevokeAllSessionsAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class OnlyShiftController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var type in feature.Controllers.Where(t => t.AsType() != typeof(ShiftPlanPreviewController)).ToArray()) feature.Controllers.Remove(type);
        }
    }
}
