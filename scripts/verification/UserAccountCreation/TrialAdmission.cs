using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace UC004Trial;

// An operator-provided receipt is a binding to external approval, not an approval issuer.
public sealed record Receipt
{
    public string Owner { get; init; } = "";
    public string ApprovalReference { get; init; } = "";
    public bool ExecutionApproved { get; init; }
    public string Host { get; init; } = "";
    public string Server { get; init; } = "";
    public string Database { get; init; } = "";
    public Guid DatabaseGuid { get; init; }
    public string Principal { get; init; } = "";
    public string PrincipalSidHex { get; init; } = "";
    public string PrincipalApprovalReference { get; init; } = "";
    public string SchemaApprovalReference { get; init; } = "";
    public string FixtureApprovalReference { get; init; } = "";
    public string IsolationMarker { get; init; } = "";
    public bool Disposable { get; init; }
    public bool FixtureOwned { get; init; }
    public bool Exclusive { get; init; }
    public bool RollbackRequired { get; init; }
    public string LeaseId { get; init; } = "";
    public DateTimeOffset StartsUtc { get; init; }
    public DateTimeOffset EndsUtc { get; init; }
    public string Mode { get; init; } = "";
    public string SchemaPath { get; init; } = "";
    public string SchemaSha256 { get; init; } = "";
    public string FixturePath { get; init; } = "";
    public string FixtureSha256 { get; init; } = "";
    public string ApiSha256 { get; init; } = "";
    public string RunnerSha256 { get; init; } = "";
}

public sealed class AdmissionFailure(string code) : Exception(code);

public sealed record Admission(Receipt Receipt, SqlConnectionStringBuilder Connection)
{
    public CancellationTokenSource Budget(TimeSpan maximum)
    {
        TrialAdmission.Validate(Receipt, Receipt.Database, Receipt.Mode, DateTimeOffset.UtcNow);
        var remaining = Receipt.EndsUtc - DateTimeOffset.UtcNow;
        TrialAdmission.Require(remaining > TimeSpan.Zero, "ACTIVE_LEASE_REQUIRED");
        return new(remaining < maximum ? remaining : maximum);
    }
}

public static class TrialAdmission
{
    public static bool CertificateAccepted(System.Net.Security.SslPolicyErrors errors, string? actual, string expected)
        => errors == System.Net.Security.SslPolicyErrors.None && !string.IsNullOrEmpty(actual) && actual == expected;
    public static void Require(bool valid, string code)
    {
        if (!valid) throw new AdmissionFailure(code);
    }

    public static void Arguments(string[] args, string mode)
    {
        var count = mode == "HTTP_LEGACY" ? 3 : 2;
        Require(args.Length == count, "REQUIRED_ARGUMENTS_MISSING");
        var database = mode == "ENTITY_READ_ONLY" ? args[1] : args[0];
        Require(Regex.IsMatch(database, @"\AHotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh\z"), "INVALID_TRIAL_TARGET");
        if (mode != "ENTITY_READ_ONLY")
            Require(!string.IsNullOrWhiteSpace(args[1]) && !File.Exists(args[1]) && !Directory.Exists(args[1]), "FRESH_EVIDENCE_PATH_REQUIRED");
    }

    public static void Validate(Receipt r, string database, string mode, DateTimeOffset now)
    {
        Require(r.ExecutionApproved && !string.IsNullOrWhiteSpace(r.Owner) && !string.IsNullOrWhiteSpace(r.ApprovalReference), "EXECUTION_APPROVAL_REQUIRED");
        Require(r.Host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase), "EXACT_HOST_REQUIRED");
        Require(r.Database == database && Regex.IsMatch(database, @"\AHotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh\z")
            && r.DatabaseGuid != Guid.Empty && !string.IsNullOrWhiteSpace(r.Server), "EXACT_TRIAL_TARGET_REQUIRED");
        Require(r.Disposable && r.FixtureOwned && r.Exclusive && r.RollbackRequired && r.IsolationMarker.Length >= 16, "ISOLATED_OWNED_FIXTURE_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(r.Principal) && !r.Principal.Equals("sa", StringComparison.OrdinalIgnoreCase), "RESTRICTED_PRINCIPAL_REQUIRED");
        Require(Regex.IsMatch(r.PrincipalSidHex, @"\A(?:[0-9a-fA-F]{2})+\z") && !string.IsNullOrWhiteSpace(r.PrincipalApprovalReference), "APPROVED_PRINCIPAL_IDENTITY_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(r.LeaseId) && r.StartsUtc <= now && now < r.EndsUtc, "ACTIVE_LEASE_REQUIRED");
        Require(r.Mode == mode && mode is "PARTICIPANT" or "ENTITY_READ_ONLY", "EXACT_EXECUTION_SCOPE_REQUIRED");
        Require(new[] { r.SchemaPath, r.SchemaSha256, r.FixturePath, r.FixtureSha256, r.ApiSha256, r.RunnerSha256 }.All(x => !string.IsNullOrWhiteSpace(x)), "APPROVED_ARTIFACTS_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(r.SchemaApprovalReference) && !string.IsNullOrWhiteSpace(r.FixtureApprovalReference), "ARTIFACT_OWNER_APPROVAL_REQUIRED");
    }

    public static SqlConnectionStringBuilder Connection(Receipt r, string value)
    {
        SqlConnectionStringBuilder c;
        try { c = new(value); }
        catch (ArgumentException) { throw new AdmissionFailure("INVALID_SQL_CONFIGURATION"); }
        Require(!c.TrustServerCertificate && c.Encrypt.ToString() is "True" or "Mandatory" or "Strict", "VERIFIED_SQL_TLS_REQUIRED");
        Require(c.DataSource.Equals(r.Server, StringComparison.OrdinalIgnoreCase) && c.InitialCatalog == r.Database
            && c.AttachDBFilename == "" && c.FailoverPartner == "" && c.HostNameInCertificate == "" && c.ServerCertificate == ""
            && !c.PersistSecurityInfo && c.ApplicationIntent == ApplicationIntent.ReadWrite, "EXACT_SQL_TARGET_REQUIRED");
        Require(c.IntegratedSecurity || (c.UserID.Equals(r.Principal, StringComparison.OrdinalIgnoreCase) && c.Password.Length > 0), "RESTRICTED_PRINCIPAL_REQUIRED");
        c.ConnectTimeout = 10;
        c.ConnectRetryCount = 0;
        c.ApplicationName = "HotelOps UC004 isolated trial";
        return c;
    }

    public static void Artifact(string path, string hash)
    {
        Require(Regex.IsMatch(hash, @"\A[0-9a-fA-F]{64}\z") && File.Exists(path), "APPROVED_ARTIFACTS_REQUIRED");
        Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(hash, StringComparison.OrdinalIgnoreCase), "ARTIFACT_HASH_MISMATCH");
    }

    public static Admission Load(string[] args, string mode)
    {
        Arguments(args, mode);
        // The historical HTTP suite also changes schema, principals and other UCs.
        // Its scope cannot be authorized by a UC004-only lease; reject before any connection.
        Require(mode != "HTTP_LEGACY", "LEGACY_HTTP_REQUIRES_SCOPED_REFACTOR");
        Require(Environment.GetEnvironmentVariable("UC004_EXECUTE") == "APPROVED", "EXECUTION_AUTHORIZATION_REQUIRED");
        var path = Environment.GetEnvironmentVariable("UC004_RECEIPT") ?? "";
        Artifact(path, Environment.GetEnvironmentVariable("UC004_RECEIPT_SHA256") ?? "");
        var r = JsonSerializer.Deserialize<Receipt>(File.ReadAllBytes(path), new JsonSerializerOptions
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow }) ?? throw new AdmissionFailure("INVALID_RECEIPT");
        var database = mode == "ENTITY_READ_ONLY" ? args[1] : args[0];
        Validate(r, database, mode, DateTimeOffset.UtcNow);
        Artifact(r.SchemaPath, r.SchemaSha256);
        Artifact(r.FixturePath, r.FixtureSha256);
        Artifact(typeof(HotelOps.Api.Data.HotelOpsDbContext).Assembly.Location, r.ApiSha256);
        Artifact(typeof(TrialAdmission).Assembly.Location, r.RunnerSha256);
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(r.FixturePath));
        var f = fixture.RootElement;
        Require(f.GetProperty("approved").GetBoolean() && f.GetProperty("databaseGuid").GetGuid() == r.DatabaseGuid
            && f.GetProperty("owner").GetString() == r.Owner && f.GetProperty("leaseId").GetString() == r.LeaseId
            && f.GetProperty("mode").GetString() == mode, "FIXTURE_APPROVAL_BINDING_REQUIRED");
        return new(r, Connection(r, Environment.GetEnvironmentVariable("UC004_TRIAL_SQL") ?? ""));
    }

    // Read-only live proof precedes fixture DML; keep this connection open for the session lease lock.
    public static async Task VerifyAsync(DbConnection connection, Admission admission, CancellationToken ct)
    {
        var r = admission.Receipt;
        Validate(r, r.Database, r.Mode, DateTimeOffset.UtcNow);
        try { await connection.OpenAsync(ct); }
        catch (SqlException) { throw new AdmissionFailure("SQL_TLS_OR_CONNECTION_FAILED"); }
        using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = """
            SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),
              (SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()),ORIGINAL_LOGIN(),
              IS_SRVROLEMEMBER('sysadmin'),IS_MEMBER('db_owner'),HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'),
              HAS_PERMS_BY_NAME(NULL,'SERVER','CONTROL SERVER'),
              (SELECT CONVERT(nvarchar(256),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UC004.Trial'),
              SUSER_SID(ORIGINAL_LOGIN()),IS_MEMBER('db_ddladmin'),IS_MEMBER('db_securityadmin'),
              HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),HAS_PERMS_BY_NAME(NULL,'SERVER','IMPERSONATE ANY LOGIN')
            """;
        await using (var row = await command.ExecuteReaderAsync(ct))
        {
            Require(await row.ReadAsync(ct) && !Enumerable.Range(0, 14).Any(row.IsDBNull), "LIVE_TRIAL_PROOF_REQUIRED");
            Require(row.GetString(0).Equals(r.Server, StringComparison.OrdinalIgnoreCase) && row.GetString(1) == r.Database
                && row.GetGuid(2) == r.DatabaseGuid && row.GetString(3).Equals(r.Principal, StringComparison.OrdinalIgnoreCase), "LIVE_TARGET_PRINCIPAL_MISMATCH");
            Require(Convert.ToHexString(row.GetFieldValue<byte[]>(9)).Equals(r.PrincipalSidHex, StringComparison.OrdinalIgnoreCase), "LIVE_PRINCIPAL_SID_MISMATCH");
            Require(Enumerable.Range(4, 4).Concat(Enumerable.Range(10, 4)).All(i => row.GetInt32(i) == 0)
                && row.GetString(8) == r.IsolationMarker, "RESTRICTED_ISOLATED_TARGET_REQUIRED");
        }
        Validate(r, r.Database, r.Mode, DateTimeOffset.UtcNow);
        command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.UC004.Trial',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; SELECT @r;";
        Require(await command.ExecuteScalarAsync(ct) is int result && result >= 0, "EXCLUSIVE_RUNNER_REQUIRED");
    }
}
