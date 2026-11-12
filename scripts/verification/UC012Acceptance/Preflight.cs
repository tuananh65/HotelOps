using HotelOps.Api.Controllers;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography.X509Certificates;

namespace UC012Acceptance;

public static class Preflight
{
    public static void Offline(TrialReceipt r)
    {
        Guards.Validate(r, DateTimeOffset.UtcNow);
        Guards.Artifact(r.SchemaArtifact, r.SchemaSha256);
        Guards.Artifact(r.FixtureArtifact, r.FixtureSha256);
        using var fixture = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(r.FixtureArtifact));
        var f = fixture.RootElement;
        Guards.Require(f.GetProperty("owner").GetString() == r.Owner && f.GetProperty("databaseGuid").GetGuid() == r.DatabaseGuid
            && f.GetProperty("leaseId").GetString() == r.LeaseId && f.GetProperty("actorId").GetInt64() == r.ActorId
            && f.GetProperty("deniedActorId").GetInt64() == r.DeniedActorId && f.GetProperty("codePrefix").GetString() == r.CodePrefix
            && f.GetProperty("approved").GetBoolean(), "FIXTURE_RECEIPT_BINDING_REQUIRED");
        Guards.Artifact(r.FrontendVerificationArtifact, r.FrontendVerificationSha256);
        using var frontend = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(r.FrontendVerificationArtifact));
        Guards.Require(frontend.RootElement.GetProperty("status").GetString() == "PASS"
            && frontend.RootElement.GetProperty("candidateAssemblySha256").GetString() == r.CandidateAssemblySha256
            && frontend.RootElement.GetProperty("frontendContractsSha256").GetString() is { Length: 64 }
            && frontend.RootElement.GetProperty("testsPassed").GetInt32() > 0, "MATCHING_FRONTEND_ACCEPTANCE_REQUIRED");
        Guards.Artifact(typeof(BranchAdministrationService).Assembly.Location, r.CandidateAssemblySha256);
        Guards.Require(typeof(BranchMaintenanceController).GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(BranchAdministrationService))), "PR51_CONTROLLER_CUTOVER_REQUIRED");
        Guards.Require(OperatingSystem.IsWindows(), "WINDOWS_DPAPI_REQUIRED");
    }
    public static X509Certificate2 Certificate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser); store.Open(OpenFlags.ReadOnly);
        foreach (var cert in store.Certificates)
        {
            if (!cert.HasPrivateKey || cert.NotAfter.ToUniversalTime() <= DateTime.UtcNow || cert.NotBefore.ToUniversalTime() > DateTime.UtcNow) continue;
            if (!cert.Extensions.OfType<X509Extension>().Any(e => e.Oid?.Value == "1.3.6.1.4.1.311.84.1.1")) continue;
            using var chain = new X509Chain();
            chain.ChainPolicy.ApplicationPolicy.Add(new("1.3.6.1.5.5.7.3.1"));
            if (chain.Build(cert) && cert.GetNameInfo(X509NameType.DnsName, false) == "localhost") return cert;
        }
        throw new GuardFailure("TRUSTED_LOCALHOST_HTTPS_CERT_REQUIRED");
    }
    public static async Task SqlAsync(TrialReceipt r, string connection, CancellationToken ct)
    {
        Guards.Validate(r, DateTimeOffset.UtcNow); Guards.Connection(r, connection);
        await using var sql = new SqlConnection(connection); await sql.OpenAsync(ct);
        await using var cmd = sql.CreateCommand(); cmd.CommandTimeout = 15;
        cmd.CommandText = """
            SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),
            (SELECT database_guid FROM sys.database_recovery_status WHERE database_id=DB_ID()),
            ORIGINAL_LOGIN(),IS_SRVROLEMEMBER('sysadmin'),IS_MEMBER('db_owner'),
            (SELECT CONVERT(nvarchar(256),value) FROM sys.extended_properties WHERE class=0 AND name='HotelOps.UC012.Trial'),
            HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'),HAS_PERMS_BY_NAME(NULL,'SERVER','CONTROL SERVER')
            """;
        await using (var row = await cmd.ExecuteReaderAsync(ct))
        {
            Guards.Require(await row.ReadAsync(ct), "SQL_IDENTITY_ROW_REQUIRED");
            Guards.Require(row.GetString(0).Equals(r.Server, StringComparison.OrdinalIgnoreCase) && row.GetString(1) == r.Database && row.GetGuid(2) == r.DatabaseGuid, "SQL_IDENTITY_MISMATCH");
            Guards.Require(row.GetString(3).Equals(r.SqlPrincipal, StringComparison.OrdinalIgnoreCase) && !row.IsDBNull(4) && row.GetInt32(4) == 0 && !row.IsDBNull(5) && row.GetInt32(5) == 0
                && !row.IsDBNull(6) && row.GetString(6) == r.IsolationMarker && !row.IsDBNull(7) && row.GetInt32(7) == 0 && !row.IsDBNull(8) && row.GetInt32(8) == 0, "RESTRICTED_TRIAL_PRINCIPAL_REQUIRED");
        }
        // Same schema predicate as BranchAdministrationSchema; no DDL or fixture writes.
        using var schema = new StreamReader(typeof(Preflight).Assembly.GetManifestResourceStream("UC012Acceptance.schema-precheck.sql")!);
        cmd.CommandText = await schema.ReadToEndAsync(ct);
        Guards.Require(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1, "BRANCH_SCHEMA_INCOMPATIBLE");
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.users WHERE deleted_at IS NULL AND account_status='ACTIVE' AND employment_status='ACTIVE' AND ((user_id=@a AND username=@u) OR (user_id=@b AND username=@v))";
        cmd.Parameters.AddWithValue("@a", r.ActorId); cmd.Parameters.AddWithValue("@b", r.DeniedActorId); cmd.Parameters.AddWithValue("@u", r.ActorUsername); cmd.Parameters.AddWithValue("@v", r.DeniedUsername);
        Guards.Require(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 2, "OWNED_ACTIVE_FIXTURES_REQUIRED");
        cmd.Parameters.Clear(); cmd.CommandText = "SELECT COUNT(*) FROM dbo.branches WHERE branch_code LIKE @prefix"; cmd.Parameters.AddWithValue("@prefix", r.CodePrefix + "%");
        Guards.Require(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 0, "FRESH_PREFIX_REQUIRED_NO_REPLAY");
    }
}
