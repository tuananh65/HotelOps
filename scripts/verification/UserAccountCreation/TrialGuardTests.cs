using System.Net.Security;

namespace UC004Trial;

public static class TrialGuardTests
{
    public static void Run()
    {
        var passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + name);
            passed++;
            Console.WriteLine("PASS: " + name);
        }
        void Reject(Action action, string code)
        {
            try { action(); }
            catch (AdmissionFailure error) { Check(error.Message == code, code); return; }
            throw new InvalidOperationException("FAIL: expected " + code);
        }
        var now = DateTimeOffset.UtcNow;
        var r = new Receipt
        {
            Owner = "offline-test",
            ApprovalReference = "synthetic-not-execution-approval",
            ExecutionApproved = true,
            Host = Environment.MachineName,
            Server = "isolated.invalid",
            Database = "HotelOps_Canonical_20261113_120000_abcdef12_Fresh",
            DatabaseGuid = Guid.NewGuid(),
            Principal = "trial_user",
            IsolationMarker = "offline-test-marker",
            Disposable = true,
            FixtureOwned = true,
            Exclusive = true,
            RollbackRequired = true,
            LeaseId = "offline",
            StartsUtc = now.AddMinutes(-1),
            EndsUtc = now.AddMinutes(1),
            Mode = "PARTICIPANT",
            SchemaPath = "offline.sql",
            SchemaSha256 = new('A', 64),
            FixturePath = "offline.json",
            PrincipalSidHex = "01AB",
            PrincipalApprovalReference = "offline-only",
            SchemaApprovalReference = "offline-only",
            FixtureApprovalReference = "offline-only",
            FixtureSha256 = new('B', 64),
            ApiSha256 = new('C', 64),
            RunnerSha256 = new('D', 64)
        };
        void Validate(Receipt value) => TrialAdmission.Validate(value, r.Database, "PARTICIPANT", now);
        Validate(r); Check(true, "valid offline receipt shape is not a live authorization");
        Reject(() => TrialAdmission.Arguments([], "PARTICIPANT"), "REQUIRED_ARGUMENTS_MISSING");
        Reject(() => TrialAdmission.Arguments(["HotelOpsDB", "new.json"], "PARTICIPANT"), "INVALID_TRIAL_TARGET");
        Reject(() => TrialAdmission.Arguments([r.Database + "\n", "new.json"], "PARTICIPANT"), "INVALID_TRIAL_TARGET");
        Reject(() => Validate(r with { ExecutionApproved = false }), "EXECUTION_APPROVAL_REQUIRED");
        Reject(() => Validate(r with { ApprovalReference = "" }), "EXECUTION_APPROVAL_REQUIRED");
        Reject(() => Validate(r with { Host = "another-host" }), "EXACT_HOST_REQUIRED");
        Reject(() => Validate(r with { DatabaseGuid = Guid.Empty }), "EXACT_TRIAL_TARGET_REQUIRED");
        Reject(() => Validate(r with { Database = "HotelOps_ImportReview" }), "EXACT_TRIAL_TARGET_REQUIRED");
        Reject(() => Validate(r with { Disposable = false }), "ISOLATED_OWNED_FIXTURE_REQUIRED");
        Reject(() => Validate(r with { FixtureOwned = false }), "ISOLATED_OWNED_FIXTURE_REQUIRED");
        Reject(() => Validate(r with { Exclusive = false }), "ISOLATED_OWNED_FIXTURE_REQUIRED");
        Reject(() => Validate(r with { RollbackRequired = false }), "ISOLATED_OWNED_FIXTURE_REQUIRED");
        Reject(() => Validate(r with { Principal = "" }), "RESTRICTED_PRINCIPAL_REQUIRED");
        Reject(() => Validate(r with { Principal = "sa" }), "RESTRICTED_PRINCIPAL_REQUIRED");
        Reject(() => Validate(r with { PrincipalSidHex = "" }), "APPROVED_PRINCIPAL_IDENTITY_REQUIRED");
        Reject(() => Validate(r with { PrincipalApprovalReference = "" }), "APPROVED_PRINCIPAL_IDENTITY_REQUIRED");
        Reject(() => Validate(r with { SchemaApprovalReference = "" }), "ARTIFACT_OWNER_APPROVAL_REQUIRED");
        Reject(() => Validate(r with { FixtureApprovalReference = "" }), "ARTIFACT_OWNER_APPROVAL_REQUIRED");
        Reject(() => Validate(r with { LeaseId = "" }), "ACTIVE_LEASE_REQUIRED");
        Reject(() => Validate(r with { EndsUtc = now }), "ACTIVE_LEASE_REQUIRED");
        Reject(() => Validate(r with { StartsUtc = now.AddSeconds(1) }), "ACTIVE_LEASE_REQUIRED");
        Reject(() => Validate(r with { SchemaSha256 = "" }), "APPROVED_ARTIFACTS_REQUIRED");
        Reject(() => Validate(r with { FixturePath = "" }), "APPROVED_ARTIFACTS_REQUIRED");
        Reject(() => Validate(r with { Mode = "HTTP_LEGACY" }), "EXACT_EXECUTION_SCOPE_REQUIRED");
        var sql = $"Server={r.Server};Database={r.Database};Integrated Security=true;Encrypt=true;TrustServerCertificate=false";
        var safe = TrialAdmission.Connection(r, sql);
        Check(!safe.TrustServerCertificate && safe.ConnectRetryCount == 0, "verified TLS, no automatic connection retry");
        Reject(() => TrialAdmission.Connection(r, sql + ";TrustServerCertificate=true"), "VERIFIED_SQL_TLS_REQUIRED");
        Reject(() => TrialAdmission.Connection(r, sql + ";Encrypt=false"), "VERIFIED_SQL_TLS_REQUIRED");
        Reject(() => TrialAdmission.Connection(r, sql + ";Database=HotelOpsDB"), "EXACT_SQL_TARGET_REQUIRED");
        Reject(() => TrialAdmission.Connection(r, sql + ";Server=other.invalid"), "EXACT_SQL_TARGET_REQUIRED");
        Reject(() => TrialAdmission.Connection(r, sql + ";HostNameInCertificate=other.invalid"), "EXACT_SQL_TARGET_REQUIRED");
        Reject(() => TrialAdmission.Connection(r, sql + ";ServerCertificate=override.cer"), "EXACT_SQL_TARGET_REQUIRED");
        Reject(() => TrialAdmission.Connection(r, sql + ";Persist Security Info=true"), "EXACT_SQL_TARGET_REQUIRED");
        Reject(() => TrialAdmission.Connection(r, sql + ";Integrated Security=false;User ID=sa;Password=synthetic"), "RESTRICTED_PRINCIPAL_REQUIRED");
        Reject(() => TrialAdmission.Connection(r, "malformed"), "INVALID_SQL_CONFIGURATION");
        foreach (var errors in new[] { SslPolicyErrors.RemoteCertificateChainErrors, SslPolicyErrors.RemoteCertificateNameMismatch,
                     SslPolicyErrors.RemoteCertificateNotAvailable, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch })
            Check(!TrialAdmission.CertificateAccepted(errors, "same", "same"), "certificate rejected offline: " + errors);
        Check(!TrialAdmission.CertificateAccepted(SslPolicyErrors.None, "unexpected", "expected"), "wrong HTTPS pin rejected");
        Check(TrialAdmission.CertificateAccepted(SslPolicyErrors.None, "same", "same"), "HTTPS requires normal validation AND pin");
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "synthetic artifact");
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
            TrialAdmission.Artifact(file, hash); Check(true, "exact artifact bytes");
            Reject(() => TrialAdmission.Artifact(file, new('0', 64)), "ARTIFACT_HASH_MISMATCH");
            Reject(() => TrialAdmission.Artifact(file + ".missing", hash), "APPROVED_ARTIFACTS_REQUIRED");
            Reject(() => TrialAdmission.Arguments([r.Database, file], "PARTICIPANT"), "FRESH_EVIDENCE_PATH_REQUIRED");
        }
        finally { File.Delete(file); }
        var original = Environment.GetEnvironmentVariable("UC004_EXECUTE");
        try
        {
            Environment.SetEnvironmentVariable("UC004_EXECUTE", "NOT_APPROVED");
            Reject(() => TrialAdmission.Load([r.Database, Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")], "PARTICIPANT"), "EXECUTION_AUTHORIZATION_REQUIRED");
            Reject(() => TrialAdmission.Load([r.Database, "unwritten.json", "other"], "HTTP_LEGACY"), "LEGACY_HTTP_REQUIRES_SCOPED_REFACTOR");
        }
        finally { Environment.SetEnvironmentVariable("UC004_EXECUTE", original); }
        Console.WriteLine($"PASS: {passed} offline guards; SQL/HTTPS NOT_RUN; no connection or seeding invoked.");
    }
}
