using ScheduledWorkdayTrial;

// Offline artifact generation and verification never open SQL, load a certificate or start a listener.
try
{
    if (args is ["--emit-package", var output]) { TrialArtifacts.Emit(output); Console.WriteLine("CANDIDATE_EMITTED_NOT_APPROVED; SQL/HTTPS NOT_RUN"); return 0; }
    if (args is ["--self-test"]) return TrialSelfTests.Run();
    if (args is not ["--serve", var path, var sha] || Environment.GetEnvironmentVariable("UC139_TRIAL_HOST_ENABLED") != "APPROVED")
    { Console.WriteLine("DISABLED: --serve receipt approved-sha + UC139_TRIAL_HOST_ENABLED=APPROVED required"); return 2; }
    var receipt = await TrialConfiguration.ReadAsync(path, sha);
    var origin = receipt.Validate(true, DateTimeOffset.UtcNow);
    TrialArtifacts.ValidatePackage(receipt.TrialPackageManifest, receipt.TrialPackageSha256, receipt.SchemaArtifact, receipt.FixtureArtifact);
    if (!TrialConfiguration.Hash(await File.ReadAllBytesAsync(receipt.SchemaArtifact)).Equals(receipt.SchemaSha256, StringComparison.OrdinalIgnoreCase)
        || !TrialConfiguration.Hash(await File.ReadAllBytesAsync(receipt.FixtureArtifact)).Equals(receipt.FixtureSha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
    var connection = Environment.GetEnvironmentVariable("UC139_TRIAL_SQL") ?? throw new InvalidOperationException();
    var username = Environment.GetEnvironmentVariable("UC139_TRIAL_USERNAME") ?? throw new InvalidOperationException();
    if (string.IsNullOrWhiteSpace(username)) throw new InvalidOperationException();
    using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await receipt.VerifyLiveIdentityAsync(connection, budget.Token);
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
    // No HTTP endpoint, URL env override, remote listener or certificate-validation bypass.
    builder.WebHost.ConfigureKestrel(o =>
    {
        // Discard inherited Kestrel:Endpoints, not just ASPNETCORE_URLS. No extra shared/HTTP listeners.
        o.Configure(new ConfigurationBuilder().Build());
        o.ListenLocalhost(origin.Port, listen =>
        {
            var certificate = Environment.GetEnvironmentVariable("UC139_TRIAL_HTTPS_PFX");
            if (string.IsNullOrWhiteSpace(certificate)) listen.UseHttps();
            else listen.UseHttps(certificate, Environment.GetEnvironmentVariable("UC139_TRIAL_HTTPS_PFX_PASSWORD"));
        });
    });
    TrialHost.Register(builder, receipt, receipt.ValidateConnection(connection), username);
    await using var app = builder.Build(); TrialHost.Pipeline(app, receipt);
    using var leaseStop = new CancellationTokenSource(receipt.LeaseEndsUtc - DateTimeOffset.UtcNow);
    leaseStop.Token.Register(app.Lifetime.StopApplication);
    Console.WriteLine("APPROVED_DISPOSABLE_TRIAL_ONLY; no production activation");
    await app.RunAsync(); return 0;
}
catch { Console.WriteLine("TRIAL_HOST_FAILED_CLOSED; no raw exception or credential output"); return 1; }
