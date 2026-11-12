using System.Text.Json;
using UC012Fixtures;

if (args is ["--self-test"]) return SelfTests.Run();
if (args is not ["--seed", var path, var hash, var output])
{
    Console.WriteLine("SOURCE_CANDIDATE_DISABLED_BY_DEFAULT; --self-test | --seed approved-plan sha256 NEW-receipt");
    return 2;
}
try
{
    // Approval validation precedes secret reads and every SQL operation.
    PlanGuard.Artifact(path, hash);
    var plan = JsonSerializer.Deserialize<FixturePlan>(File.ReadAllBytes(path), PlanGuard.Json)
        ?? throw new FixtureGuardException("PLAN_INVALID");
    PlanGuard.Validate(plan, DateTimeOffset.UtcNow, Environment.MachineName);
    PlanGuard.Require(!File.Exists(output) && Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(output))), "NEW_RECEIPT_LOCATION_REQUIRED");
    var sql = Environment.GetEnvironmentVariable("UC012_FIXTURE_SQL") ?? throw new FixtureGuardException("PROVISIONER_CONNECTION_REQUIRED");
    var allowed = Environment.GetEnvironmentVariable("UC012_ACTOR_PASSWORD") ?? throw new FixtureGuardException("SECRETS_REQUIRED");
    var denied = Environment.GetEnvironmentVariable("UC012_DENIED_PASSWORD") ?? throw new FixtureGuardException("SECRETS_REQUIRED");
    using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var receipt = await Seeder.SeedAsync(plan, hash, sql, allowed, denied, budget.Token);
    await using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await JsonSerializer.SerializeAsync(file, receipt, PlanGuard.Json);
    Console.WriteLine("FIXTURE_COMMITTED; NO_SESSION_SEEDED; HTTPS_ACCEPTANCE_NOT_RUN");
    return 0;
}
catch (FixtureGuardException e) { Console.WriteLine(e.Message); return 1; }
catch { Console.WriteLine("SEED_OUTCOME_UNCONFIRMED; NO_RETRY; OWNER_MUST_INSPECT_FIXTURE_MARKER"); return 1; }
