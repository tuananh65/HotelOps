using System.Net;
using System.Net.Security;

namespace UC004Trial;

public static class CreateOnlyTests
{
    public static async Task RunAsync()
    {
        var count = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("FAIL: " + name);
            count++;
        }
        var receipt = new Receipt { ApiSha256 = new('A', 64), DatabaseGuid = Guid.NewGuid(), LeaseId = "offline" };
        var fixture = new HttpFixture("https://trial.invalid", new('B', 64), "offline-not-runtime-approval",
            receipt.ApiSha256, receipt.DatabaseGuid, receipt.LeaseId, "UC004_abcdef123456_", 1, 2, 3, 4,
            new(1, "syntheticAllowed", "UC004_SECRET_ALLOWED"), new(2, "syntheticDenied", "UC004_SECRET_DENIED"),
            new(3, "syntheticBranch", "UC004_SECRET_BRANCH"), CreateOnlyRunner.Operations);
        Check(CreateOnlyRunner.Validate(fixture, receipt).Scheme == "https", "valid-shape-only");
        foreach (var invalid in new[] {
            fixture with { Origin = "http://trial.invalid" }, fixture with { Origin = "https://user:pass@trial.invalid" },
            fixture with { Origin = "https://trial.invalid/path" }, fixture with { Origin = "https://trial.invalid/?token=a" },
            fixture with { CertificateSha256 = "" }, fixture with { DeploymentApproval = "" },
            fixture with { DeployedApiSha256 = new('C',64) }, fixture with { DatabaseGuid = Guid.NewGuid() },
            fixture with { LeaseId = "other" }, fixture with { Prefix = "shared" }, fixture with { BranchId = 0 },
            fixture with { OtherBranchId = fixture.BranchId }, fixture with { AllowedOperations = ["DDL"] },
            fixture with { Allowed = fixture.Denied }, fixture with { Allowed = fixture.Allowed with { PasswordEnvironment = "PLAINTEXT" } }
        })
        {
            try { CreateOnlyRunner.Validate(invalid, receipt); throw new InvalidOperationException("Accepted invalid fixture"); }
            catch (AdmissionFailure) { Check(true, "invalid-fixture-rejected-before-network"); }
        }
        foreach (var errors in new[] { SslPolicyErrors.RemoteCertificateChainErrors, SslPolicyErrors.RemoteCertificateNameMismatch,
            SslPolicyErrors.RemoteCertificateNotAvailable, SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors })
            Check(!CreateOnlyRunner.CertificateAccepted(errors, fixture.CertificateSha256, fixture.CertificateSha256), "tls-errors-rejected");
        Check(!CreateOnlyRunner.CertificateAccepted(SslPolicyErrors.None, new('C', 64), fixture.CertificateSha256), "pin-mismatch");
        Check(CreateOnlyRunner.CertificateAccepted(SslPolicyErrors.None, fixture.CertificateSha256.ToLowerInvariant(), fixture.CertificateSha256), "verified-chain-host-pin-predicate");
        var handler = new Probe();
        using var http = new HttpClient(handler) { BaseAddress = new Uri(fixture.Origin) };
        using (var result = await CreateOnlyRunner.SendAsync(http, HttpMethod.Post, "/api/users", new { employeeCode = "synthetic" }, "offline-token", CancellationToken.None))
            Check(result.StatusCode == HttpStatusCode.ServiceUnavailable && handler.Calls == 1, "503-never-retried");
        Check(handler.Method == HttpMethod.Post && handler.Path == "/api/users" && handler.HasCsrf, "exact-create-http-shape");
        foreach (var (method, path) in new[] {
            (HttpMethod.Delete, "/api/users"), (HttpMethod.Put, "/api/users"),
            (HttpMethod.Get, "/api/auth/login"), (HttpMethod.Post, "/api/auth/csrf") })
        {
            var rejected = false;
            try { using var response = await CreateOnlyRunner.SendAsync(http, method, path, null, null, CancellationToken.None); }
            catch (AdmissionFailure) { rejected = true; }
            Check(rejected && handler.Calls == 1, "unsupported-method-not-dispatched:" + method + path);
        }
        handler.Fail = true;
        try { await CreateOnlyRunner.SendAsync(http, HttpMethod.Post, "/api/users", new { }, "offline-token", CancellationToken.None); throw new InvalidOperationException("Transport failure was swallowed"); }
        catch (HttpRequestException) { Check(handler.Calls == 2, "unknown-outcome-never-replayed"); }
        try { await CreateOnlyRunner.SendAsync(http, HttpMethod.Post, "/api/users/1/reset-password", new { }, null, CancellationToken.None); throw new InvalidOperationException("Unrelated route was accepted"); }
        catch (AdmissionFailure) { Check(handler.Calls == 2, "unrelated-route-never-dispatched"); }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await CreateOnlyRunner.SendAsync(http, HttpMethod.Post, "/api/users", new { }, null, cancelled.Token); throw new InvalidOperationException("Cancellation was ignored"); }
        catch (OperationCanceledException) { Check(handler.Calls == 2, "cancelled-request-not-dispatched"); }
        if (count != 31) throw new InvalidOperationException("Incomplete offline HTTP suite");
        Console.WriteLine($"UC004 HTTP offline: {count}/31 PASS; SQL/HTTPS acceptance NOT_RUN.");
    }
    private sealed class Probe : HttpMessageHandler
    {
        public int Calls; public bool Fail; public HttpMethod? Method; public string? Path; public bool HasCsrf;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls++; Method = request.Method; Path = request.RequestUri!.AbsolutePath;
            HasCsrf = request.Headers.Contains("X-CSRF-TOKEN");
            if (Fail) throw new HttpRequestException("offline transport fault");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
