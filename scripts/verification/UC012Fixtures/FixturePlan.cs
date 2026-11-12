using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace UC012Fixtures;

public sealed record FixturePlan
{
    public string Owner { get; init; } = "";
    public string ApprovalReference { get; init; } = "";
    public string SecurityApproval { get; init; } = "";
    public bool ExecutionApproved { get; init; }
    public string Machine { get; init; } = "";
    public string Server { get; init; } = "";
    public string Database { get; init; } = "";
    public Guid DatabaseGuid { get; init; }
    public string IsolationMarker { get; init; } = "";
    public string LeaseId { get; init; } = "";
    public DateTimeOffset StartUtc { get; init; }
    public DateTimeOffset EndUtc { get; init; }
    public bool Exclusive { get; init; }
    public string RunId { get; init; } = "";
    public string CodePrefix { get; init; } = "";
    public string SchemaArtifact { get; init; } = "";
    public string SchemaSha256 { get; init; } = "";
    public string ManifestArtifact { get; init; } = "";
    public string ManifestSha256 { get; init; } = "";
    public string ApiAssemblySha256 { get; init; } = "";
}

public sealed class FixtureGuardException(string code) : Exception(code);

public static class PlanGuard
{
    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public static void Require(bool valid, string code)
    {
        if (!valid) throw new FixtureGuardException(code);
    }

    public static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));

    public static void Validate(FixturePlan p, DateTimeOffset now, string machine)
    {
        Require(p.ExecutionApproved && !string.IsNullOrWhiteSpace(p.Owner)
            && !string.IsNullOrWhiteSpace(p.ApprovalReference) && !string.IsNullOrWhiteSpace(p.SecurityApproval), "EXECUTION_APPROVAL_REQUIRED");
        Require(p.Machine.Equals(machine, StringComparison.OrdinalIgnoreCase), "EXACT_MACHINE_REQUIRED");
        Require(p.Server.Equals("MEITA\\MSSQLSERVER01", StringComparison.OrdinalIgnoreCase)
            && p.Database.StartsWith("HotelOps_UC012_Trial_", StringComparison.Ordinal)
            && p.Database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            && p.DatabaseGuid != Guid.Empty && p.IsolationMarker.Length >= 16, "DISPOSABLE_IDENTITY_REQUIRED");
        Require(p.Exclusive && p.StartUtc <= now && now < p.EndUtc
            && p.EndUtc - p.StartUtc <= TimeSpan.FromHours(1) && !string.IsNullOrWhiteSpace(p.LeaseId), "FRESH_EXCLUSIVE_LEASE_REQUIRED");
        Require(p.RunId.Length is >= 8 and <= 16 && p.RunId.All(char.IsAsciiLetterOrDigit)
            && p.CodePrefix.Length is >= 4 and <= 12 && p.CodePrefix.All(char.IsAsciiLetterOrDigit), "SAFE_FIXTURE_KEYS_REQUIRED");
    }

    public static SqlConnectionStringBuilder Connection(FixturePlan p, string value)
    {
        var c = new SqlConnectionStringBuilder(value);
        Require(c.DataSource.Equals(p.Server, StringComparison.OrdinalIgnoreCase) && c.InitialCatalog == p.Database
            && c.Encrypt.ToString() is "True" or "Mandatory" or "Strict" && !c.TrustServerCertificate
            && c.AttachDBFilename == "" && c.FailoverPartner == "" && c.HostNameInCertificate == ""
            && c.ServerCertificate == "" && c.ConnectRetryCount == 0, "VERIFIED_TLS_NO_RETRY_REQUIRED");
        return c;
    }

    public static void Artifact(string path, string hash)
        => Require(File.Exists(path) && Hash(File.ReadAllBytes(path)).Equals(hash, StringComparison.OrdinalIgnoreCase), "EXACT_ARTIFACT_REQUIRED");

    public static void Passwords(string allowed, string denied)
        => Require(allowed.Length is >= 24 and <= 128 && denied.Length is >= 24 and <= 128
            && allowed != denied && !allowed.Any(char.IsControl) && !denied.Any(char.IsControl), "DISTINCT_SECRET_INPUTS_REQUIRED");

    public static void Manifest(FixturePlan p, JsonElement root)
    {
        Require(root.GetProperty("approved").GetBoolean()
            && root.GetProperty("owner").GetString() == p.Owner
            && root.GetProperty("databaseGuid").GetGuid() == p.DatabaseGuid
            && root.GetProperty("leaseId").GetString() == p.LeaseId
            && root.GetProperty("runId").GetString() == p.RunId
            && root.GetProperty("schemaSha256").GetString() == p.SchemaSha256
            && root.GetProperty("identityProfile").GetString() == "UC012.SYS_ADMIN.UnboundVsBranch.v1", "APPROVED_MANIFEST_BINDING_REQUIRED");
    }
}
