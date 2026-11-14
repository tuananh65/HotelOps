using System.Text.Json;
using UC012Acceptance;

// No connection, certificate access, listener or credential reads on default/prepare/report/self-test.
if (args is ["--self-test"]) return SelfTests.Run();
if (args is ["--prepare", var directory])
{
    Directory.CreateDirectory(directory);
    var receipt = Path.Combine(directory, "receipt.template.json"); var manifest = Path.Combine(directory, "manifest.json");
    if (File.Exists(receipt) || File.Exists(manifest)) { Console.WriteLine("OUTPUT_EXISTS_NOT_OVERWRITTEN"); return 2; }
    await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(new TrialReceipt(), Guards.JsonOptions));
    await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
    {
        Status = "PREPARED_OFFLINE_NOT_APPROVED", CaseIds = Guards.Cases,
        RequiredMutations = Guards.Mutations, ReceiptSha256 = Guards.Hash(await File.ReadAllBytesAsync(receipt)),
        RunnerAssemblySha256 = Guards.Hash(await File.ReadAllBytesAsync(typeof(Guards).Assembly.Location)),
        ApiAssemblySha256 = Guards.Hash(await File.ReadAllBytesAsync(typeof(HotelOps.Api.Controllers.BranchMaintenanceController).Assembly.Location)),
        CandidateCutoverPresent = typeof(HotelOps.Api.Controllers.BranchMaintenanceController).GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(HotelOps.Api.Services.HotelStructure.BranchAdministrationService))),
        Sql = "NOT_RUN", Https = "NOT_RUN", Pr51 = "MERGE_HOLD"
    }, Guards.JsonOptions));
    Console.WriteLine("PREPARED_OFFLINE; NO_SQL_HTTPS; RECEIPT_REQUIRES_OWNER_INPUT"); return 0;
}
if (args is ["--report", var report, var reportHash])
{
    try
    {
        Guards.Artifact(report, reportHash); using var doc = JsonDocument.Parse(await File.ReadAllBytesAsync(report)); var root = doc.RootElement;
        var reportStatus = root.GetProperty("Status").GetString();
        Guards.Require(reportStatus is "BLOCKED" or "PREFLIGHT_PASS_EXECUTION_NOT_RUN" or "SQL_HTTPS_ACCEPTANCE_PASS"
            && root.GetProperty("RuntimeAcceptance").GetBoolean() == (reportStatus == "SQL_HTTPS_ACCEPTANCE_PASS"), "REPORT_STATUS_INVALID");
        if (root.GetProperty("RuntimeAcceptance").GetBoolean())
            Guards.Require(root.GetProperty("Status").GetString() == "SQL_HTTPS_ACCEPTANCE_PASS" && root.GetProperty("Mode").GetString() == "--execute"
                && root.GetProperty("PreflightPassed").GetBoolean() && root.GetProperty("FailureCode").GetString() == ""
                && root.GetProperty("Cases").EnumerateArray().Select(c => c.GetProperty("Id").GetString()).SequenceEqual(Guards.Cases)
                && root.GetProperty("Cases").EnumerateArray().All(c => c.GetProperty("Passed").GetBoolean()), "INCOMPLETE_ACCEPTANCE_REPORT");
        Console.WriteLine(root.GetProperty("Status").GetString()); return 0;
    }
    catch { Console.WriteLine("INVALID_REPORT"); return 1; }
}
if (args is not [var mode, var path, var sha, var output] || mode is not ("--preflight" or "--execute"))
{ Console.WriteLine("Usage: --prepare directory | --preflight|--execute receipt sha256 NEW-report.json | --report report sha256 | --self-test"); return 2; }
if (File.Exists(output)) { Console.WriteLine("REPORT_EXISTS_NOT_OVERWRITTEN"); return 2; }
var cases = new List<CaseReceipt>(); var status = "BLOCKED"; var error = ""; var preflight = false;
try
{
    var receipt = Guards.Read(path, sha); Preflight.Offline(receipt);
    var connection = Environment.GetEnvironmentVariable("UC012_TRIAL_SQL") ?? throw new GuardFailure("RESTRICTED_CONNECTION_ENV_REQUIRED");
    var remaining = receipt.LeaseEndUtc - DateTimeOffset.UtcNow;
    Guards.Require(remaining > TimeSpan.Zero, "LEASE_EXPIRED");
    using var budget = new CancellationTokenSource(remaining < TimeSpan.FromMinutes(5) ? remaining : TimeSpan.FromMinutes(5));
    await Preflight.SqlAsync(receipt, connection, budget.Token);
    using var certificate = Preflight.Certificate(); preflight = true;
    if (mode == "--preflight") status = "PREFLIGHT_PASS_EXECUTION_NOT_RUN";
    else
    {
        Guards.Require(Environment.GetEnvironmentVariable("UC012_EXECUTE") == "APPROVED", "EXECUTION_SWITCH_REQUIRED");
        await Cases.RunAsync(receipt, connection, cases, budget.Token);
        Guards.Require(cases.Count == 12 && cases.All(c => c.Passed), "INCOMPLETE_ACCEPTANCE"); status = "SQL_HTTPS_ACCEPTANCE_PASS";
    }
}
catch (GuardFailure e) { error = e.Message; }
catch { error = "TRIAL_FAILURE_SANITIZED"; }
await using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    await JsonSerializer.SerializeAsync(stream, new { Status = status, Mode = mode, ReceiptSha256 = sha,
        RunnerAssemblySha256 = Guards.Hash(await File.ReadAllBytesAsync(typeof(Guards).Assembly.Location)),
        ApiAssemblySha256 = Guards.Hash(await File.ReadAllBytesAsync(typeof(HotelOps.Api.Controllers.BranchMaintenanceController).Assembly.Location)),
        PreflightPassed = preflight, Cases = cases, FailureCode = error, RuntimeAcceptance = status == "SQL_HTTPS_ACCEPTANCE_PASS", Pr51 = "MERGE_HOLD_REVIEW_STILL_REQUIRED" }, Guards.JsonOptions);
Console.WriteLine(status); return error == "" ? 0 : 1;
