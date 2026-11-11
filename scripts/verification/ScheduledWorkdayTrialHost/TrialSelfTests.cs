using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.WorkTime;
using HotelOps.Api.Security;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ScheduledWorkdayTrial;

public static class TrialSelfTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks++; Console.WriteLine("PASS " + name); }
        void Reject(Action action, string name) { try { action(); } catch { Check(true, name); return; } throw new InvalidOperationException(name); }
        var now = DateTimeOffset.UtcNow;
        var r = new TrialConfiguration("offline", "HotelOps_UC139_Trial_OFFLINE", Guid.NewGuid(), "Offline owner", "Offline lease", now.AddMinutes(-1), now.AddMinutes(10), "NOT_APPROVED", false, false, false, "", "", "", "", "https://localhost:17019/", 1, 1, 2, 10, 11, 12, "", "", 20, 21, 22);
        Check(r.Validate(false, now).Port == 17019, "offline prepare accepts candidate without granting lease");
        Reject(() => r.Validate(true, now), "default receipt cannot serve");
        var approved = r with { ApprovalStatus = "APPROVED", ExclusiveLease = true, TrialHostMutationApproval = true };
        Check(approved.Validate(true, now).IsLoopback, "explicit bounded trial configuration");
        var http = new DefaultHttpContext(); http.Request.Scheme = "https"; http.Request.Host = new HostString("localhost", 17019); http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        Check(TrialHost.Admits(http, approved, now), "HTTP admission accepts only approved local origin");
        http.Request.Scheme = "http"; Check(!TrialHost.Admits(http, approved, now), "HTTP refused"); http.Request.Scheme = "https";
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.1"); Check(!TrialHost.Admits(http, approved, now), "remote peer refused"); http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        http.Request.Host = new HostString("other.test", 17019); Check(!TrialHost.Admits(http, approved, now), "unapproved Host refused"); http.Request.Host = new HostString("localhost", 17019);
        Check(!TrialHost.Admits(http, approved, approved.LeaseEndsUtc), "lease expiry refuses new requests");
        foreach (var bad in new[] { approved with { Database = "HotelOpsDB" }, approved with { DatabaseGuid = Guid.Empty }, approved with { LeaseEndsUtc = now }, approved with { LeaseStartsUtc = now.AddHours(-9) }, approved with { Origin = "http://localhost:17019/" }, approved with { Origin = "https://example.com/" }, approved with { AssignmentId = 11 } })
            Reject(() => bad.Validate(true, now), "invalid identity/lease/origin/fixture refuses");
        Check(approved.ValidateConnection("Server=offline;Database=HotelOps_UC139_Trial_OFFLINE;Integrated Security=true;Encrypt=true").Contains("Encrypt=True", StringComparison.OrdinalIgnoreCase), "verified TLS connection config");
        foreach (var cs in new[] { "Server=offline;Database=HotelOpsDB;Integrated Security=true;Encrypt=true", "Server=offline;Database=HotelOps_UC139_Trial_OFFLINE;Integrated Security=true;Encrypt=false", "Server=offline;Database=HotelOps_UC139_Trial_OFFLINE;Integrated Security=true;Encrypt=true;TrustServerCertificate=true" })
            Reject(() => approved.ValidateConnection(cs), "wrong database/TLS bypass rejected before SQL");
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(repository.FullName, ".git")) && !Directory.Exists(Path.Combine(repository.FullName, ".git")))
            repository = repository.Parent ?? throw new InvalidOperationException("Run self-tests inside a repository checkout");
        var directory = Path.Combine(repository.FullName, "frontend", ".test-output", "uc139-package-" + Guid.NewGuid().ToString("N"));
        TrialArtifacts.Emit(directory);
        string PathOf(string name) => Path.Combine(directory, name);
        var manifest = PathOf("package.json"); var hash = TrialConfiguration.Hash(File.ReadAllBytes(manifest));
        TrialArtifacts.ValidatePackage(manifest, hash, PathOf("01-install-candidate.sql"), PathOf("04-fixtures-candidate.sql")); Check(true, "generated five-artifact closure and hashes");
        Reject(() => TrialArtifacts.Emit(directory), "emission refuses overwrite");
        Reject(() => TrialArtifacts.ValidatePackage(manifest, new string('0', 64), PathOf("01-install-candidate.sql"), PathOf("04-fixtures-candidate.sql")), "unapproved manifest replacement rejected");
        var original = File.ReadAllText(PathOf("04-fixtures-candidate.sql")); File.AppendAllText(PathOf("04-fixtures-candidate.sql"), "-- tamper");
        Reject(() => TrialArtifacts.ValidatePackage(manifest, hash, PathOf("01-install-candidate.sql"), PathOf("04-fixtures-candidate.sql")), "fixture tamper rejected");
        File.WriteAllText(PathOf("04-fixtures-candidate.sql"), original);
        Check(File.ReadAllText(PathOf("01-install-candidate.sql")).Split("CREATE TABLE ").Length == 4, "only three mapped WorkTime tables emitted");
        Check(File.ReadAllText(PathOf("03-rollback-empty-candidate.sql")).Contains("Nonempty trial: rollback refused", StringComparison.Ordinal), "generated rollback has nonempty-data fuse (SQL NOT_RUN)");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
        TrialHost.Register(builder, r, "Server=offline;Database=offline;Integrated Security=true;Encrypt=true", "offline-user");
        using var app = builder.Build(); TrialHost.Pipeline(app, r);
        var cookie = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get(HotelOpsCookieTicket.Scheme);
        Check(cookie.Cookie.Name == "__Host-HotelOps.UC139.Trial" && cookie.Cookie.HttpOnly && cookie.Cookie.SecurePolicy == CookieSecurePolicy.Always && !cookie.SlidingExpiration, "isolated secure HttpOnly cookie without sliding expiry");
        Check(cookie.EventsType == typeof(HotelOpsCookieEvents), "real SQL cookie-validation events retained");
        Check(app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>>().Value.HeaderName == "X-CSRF-TOKEN", "real antiforgery header retained");
        var actions = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.Cast<ControllerActionDescriptor>().ToArray();
        Check(actions.Length == 8, "actual MVC discovery: exactly eight trial/auth/read actions");
        string Methods(ControllerActionDescriptor a) => string.Join(',', a.ActionConstraints!.OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods).Order());
        Check(actions.All(a => Methods(a) is "GET" or "POST"), "no PUT/PATCH/DELETE endpoints");
        Check(actions.Count(a => Methods(a) == "GET") == 4 && actions.Count(a => Methods(a) == "POST") == 4, "exact four GET/four POST trial inventory");
        Check(actions.Select(a => a.AttributeRouteInfo!.Template + "|" + Methods(a)).Distinct().Count() == actions.Length, "no duplicate method/route templates");
        Check(actions.All(a => a.ControllerTypeInfo.AsType() != typeof(WorkdaysController)), "production NonController remains absent");
        Check(typeof(WorkdaysController).IsDefined(typeof(NonControllerAttribute), true), "production NonController preserved");
        Check(actions.Count(a => a.ControllerTypeInfo.AsType() == typeof(TrialScheduledController)) == 2, "exactly two dedicated scheduled trial actions");
        Check(!actions.Any(a => a.ActionName == "ChangePassword"), "unneeded Auth write excluded");
        foreach (var action in actions.Where(a => a.ControllerTypeInfo.AsType() == typeof(TrialScheduledController)))
        {
            Check(action.ControllerTypeInfo.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().AuthenticationSchemes == HotelOpsCookieTicket.Scheme, "existing cookie scheme retained");
            Check(action.AttributeRouteInfo!.Template!.Contains("{branchId:long}", StringComparison.Ordinal), "real MVC branch route constraint");
        }
        using (var scope = app.Services.CreateScope()) { Check(scope.ServiceProvider.GetRequiredService<WorkdayService>() != null, "real WorkdayService dependency closure resolves offline"); }
        var controller = new TrialScheduledController(null!, r) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Check(controller.Start(1, new StartWorkdayRequest { Reason = "unscheduled" }, default).Result is ObjectResult { StatusCode: 400 }, "unscheduled refused without service dispatch");
        Check(controller.Start(1, new StartWorkdayRequest { ShiftAssignmentId = 12 }, default).Result is ObjectResult { StatusCode: 400 }, "rollback-only assignment cannot dispatch over HTTP");
        Check(controller.End(1, 1, new EndWorkdayRequest { PauseAll = true }, default).Result is ObjectResult { StatusCode: 400 }, "Pause All refused without service dispatch");
        Console.WriteLine($"TRIAL_PREPARATION: {checks} PASS; SQL/HTTPS execution NOT_RUN"); return 0;
    }
}
