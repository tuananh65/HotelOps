namespace UC012Fixtures;

public static class SelfTests
{
    public static int Run()
    {
        var count = 0;
        var now = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
        var p = new FixturePlan
        {
            Owner = "fixture-owner",
            ApprovalReference = "execution-record",
            SecurityApproval = "security-record",
            ExecutionApproved = true,
            Machine = "MEITA",
            Server = "MEITA\\MSSQLSERVER01",
            Database = "HotelOps_UC012_Trial_test",
            DatabaseGuid = Guid.NewGuid(),
            IsolationMarker = "synthetic-isolation-marker",
            LeaseId = "fresh-epoch",
            StartUtc = now.AddMinutes(-1),
            EndUtc = now.AddMinutes(30),
            Exclusive = true,
            RunId = "TEST1234",
            CodePrefix = "ACCEPT"
        };
        void Pass(Action test) { test(); count++; }
        void Reject(Action test, string code)
        {
            try { test(); throw new InvalidOperationException("Expected guard: " + code); }
            catch (FixtureGuardException e) when (e.Message == code) { count++; }
        }
        void Invalid(FixturePlan value, string code) => Reject(() => PlanGuard.Validate(value, now, "MEITA"), code);
        try
        {
            Pass(() => PlanGuard.Validate(p, now, "MEITA"));
            Invalid(p with { ExecutionApproved = false }, "EXECUTION_APPROVAL_REQUIRED");
            Invalid(p with { SecurityApproval = "" }, "EXECUTION_APPROVAL_REQUIRED");
            Invalid(p with { ApprovalReference = "" }, "EXECUTION_APPROVAL_REQUIRED");
            Invalid(p with { Machine = "OTHER" }, "EXACT_MACHINE_REQUIRED");
            Invalid(p with { Server = "MEITA" }, "DISPOSABLE_IDENTITY_REQUIRED");
            Invalid(p with { Database = "HotelOpsDB" }, "DISPOSABLE_IDENTITY_REQUIRED");
            Invalid(p with { Database = "HotelOps_ImportReview" }, "DISPOSABLE_IDENTITY_REQUIRED");
            Invalid(p with { Database = "HotelOps_UC012_Trial_x;DROP" }, "DISPOSABLE_IDENTITY_REQUIRED");
            Invalid(p with { DatabaseGuid = Guid.Empty }, "DISPOSABLE_IDENTITY_REQUIRED");
            Invalid(p with { IsolationMarker = "" }, "DISPOSABLE_IDENTITY_REQUIRED");
            Invalid(p with { Exclusive = false }, "FRESH_EXCLUSIVE_LEASE_REQUIRED");
            Invalid(p with { EndUtc = now }, "FRESH_EXCLUSIVE_LEASE_REQUIRED");
            Invalid(p with { StartUtc = now.AddMinutes(1) }, "FRESH_EXCLUSIVE_LEASE_REQUIRED");
            Invalid(p with { EndUtc = now.AddHours(2) }, "FRESH_EXCLUSIVE_LEASE_REQUIRED");
            Invalid(p with { LeaseId = "" }, "FRESH_EXCLUSIVE_LEASE_REQUIRED");
            Invalid(p with { RunId = "../unsafe" }, "SAFE_FIXTURE_KEYS_REQUIRED");
            Invalid(p with { CodePrefix = "A_B%" }, "SAFE_FIXTURE_KEYS_REQUIRED");
            var connection = "Server=MEITA\\MSSQLSERVER01;Database=HotelOps_UC012_Trial_test;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;ConnectRetryCount=0";
            Pass(() => PlanGuard.Connection(p, connection));
            foreach (var suffix in new[] { ";TrustServerCertificate=true", ";Encrypt=false", ";Database=HotelOpsDB", ";Server=MEITA", ";HostNameInCertificate=localhost", ";ConnectRetryCount=1" })
                Reject(() => PlanGuard.Connection(p, connection + suffix), "VERIFIED_TLS_NO_RETRY_REQUIRED");
            Pass(() => PlanGuard.Passwords(new string('A', 24), new string('B', 24)));
            Reject(() => PlanGuard.Passwords("short", new string('B', 24)), "DISTINCT_SECRET_INPUTS_REQUIRED");
            Reject(() => PlanGuard.Passwords(new string('A', 24), new string('A', 24)), "DISTINCT_SECRET_INPUTS_REQUIRED");
            Reject(() => PlanGuard.Artifact("nonexistent-owned-fixture-artifact", new string('A', 64)), "EXACT_ARTIFACT_REQUIRED");
            var manifest = new Dictionary<string, object>
            {
                ["approved"] = true,
                ["owner"] = p.Owner,
                ["databaseGuid"] = p.DatabaseGuid,
                ["leaseId"] = p.LeaseId,
                ["runId"] = p.RunId,
                ["schemaSha256"] = p.SchemaSha256,
                ["identityProfile"] = "UC012.SYS_ADMIN.UnboundVsBranch.v1"
            };
            Pass(() => PlanGuard.Manifest(p, System.Text.Json.JsonSerializer.SerializeToElement(manifest)));
            foreach (var pair in new (string Key, object Value)[] { ("approved", false), ("owner", "other"),
                ("databaseGuid", Guid.NewGuid()), ("leaseId", "old"), ("runId", "other"), ("schemaSha256", "changed"), ("identityProfile", "global-bypass") })
            {
                var changed = new Dictionary<string, object>(manifest) { [pair.Key] = pair.Value };
                Reject(() => PlanGuard.Manifest(p, System.Text.Json.JsonSerializer.SerializeToElement(changed)), "APPROVED_MANIFEST_BINDING_REQUIRED");
            }
            Console.WriteLine($"OFFLINE_GUARDS: {count} PASS; SQL_AND_HTTPS_NOT_RUN"); return 0;
        }
        catch { Console.WriteLine($"OFFLINE_GUARDS_FAILED_AFTER_{count}; SQL_AND_HTTPS_NOT_RUN"); return 1; }
    }
}
