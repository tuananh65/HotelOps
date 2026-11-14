using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace UC012Acceptance;

public sealed record TrialReceipt
{
    public string Owner { get; init; } = "";
    public string HostMachine { get; init; } = "";
    public string ApprovalReference { get; init; } = "";
    public string Server { get; init; } = "";
    public string Database { get; init; } = "";
    public Guid DatabaseGuid { get; init; }
    public string SqlPrincipal { get; init; } = "";
    public string IsolationMarker { get; init; } = "";
    public string LeaseId { get; init; } = "";
    public DateTimeOffset LeaseStartUtc { get; init; }
    public DateTimeOffset LeaseEndUtc { get; init; }
    public bool Disposable { get; init; }
    public bool ExclusiveLease { get; init; }
    public bool ExecutionApproved { get; init; }
    public bool FixtureOwned { get; init; }
    public bool RollbackRequired { get; init; }
    public int? AuditFaultStatus { get; init; }
    public string AuditFaultApproval { get; init; } = "";
    public string Origin { get; init; } = "https://localhost:7445";
    public string CandidateAssemblySha256 { get; init; } = "";
    public string SchemaArtifact { get; init; } = "";
    public string SchemaSha256 { get; init; } = "";
    public string FixtureArtifact { get; init; } = "";
    public string FixtureSha256 { get; init; } = "";
    public string FrontendVerificationArtifact { get; init; } = "";
    public string FrontendVerificationSha256 { get; init; } = "";
    public long ActorId { get; init; }
    public long DeniedActorId { get; init; }
    public string ActorUsername { get; init; } = "";
    public string DeniedUsername { get; init; } = "";
    public string CodePrefix { get; init; } = "";
    public string[] AllowedMutations { get; init; } = [];
}

public static class Guards
{
    public static readonly string[] Mutations = ["AUTH_LOGIN_LOGOUT_SESSION_TOUCH", "CREATE_BRANCH", "UPDATE_BRANCH", "EXPIRE_OWN_SESSION", "AUDIT_FAULT_ROLLBACK"];
    public static readonly string[] Cases = ["CREATE_IDENTITY_ETAG", "READ_ROWVERSION", "UPDATE_FIXED_TIMEZONE", "DUPLICATE_CODE", "IF_MATCH", "PERMISSION_ADMISSION", "CSRF", "SESSION_EXPIRY", "CONCURRENT_UPDATE", "ATOMIC_AUDIT_NOOP", "AUDIT_ROLLBACK", "ETAG_CUTOVER_NO_REPLAY"];
    public static void Require(bool condition, string code) { if (!condition) throw new GuardFailure(code); }
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static void Artifact(string path, string hash) => Require(File.Exists(path) && Hash(File.ReadAllBytes(path)).Equals(hash, StringComparison.OrdinalIgnoreCase), "ARTIFACT_IDENTITY_REQUIRED");
    public static TrialReceipt Read(string path, string hash)
    {
        Artifact(path, hash);
        return JsonSerializer.Deserialize<TrialReceipt>(File.ReadAllBytes(path), JsonOptions) ?? throw new GuardFailure("RECEIPT_INVALID");
    }
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static void Validate(TrialReceipt r, DateTimeOffset now)
    {
        Require(!string.IsNullOrWhiteSpace(r.Owner) && !string.IsNullOrWhiteSpace(r.ApprovalReference) && r.ExecutionApproved, "OWNER_APPROVAL_REQUIRED");
        Require(r.HostMachine.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase), "EXACT_HOST_MACHINE_REQUIRED");
        Require(r.Disposable && r.ExclusiveLease && r.FixtureOwned && r.RollbackRequired, "ISOLATION_FIXTURE_ROLLBACK_APPROVAL_REQUIRED");
        Require(r.Database.StartsWith("HotelOps_UC012_Trial_", StringComparison.Ordinal) && r.DatabaseGuid != Guid.Empty && !string.IsNullOrWhiteSpace(r.Server), "EXACT_DISPOSABLE_TARGET_REQUIRED");
        Require(r.IsolationMarker.Length >= 16 && !string.IsNullOrWhiteSpace(r.SqlPrincipal), "LIVE_ISOLATION_IDENTITY_REQUIRED");
        Require(!string.IsNullOrWhiteSpace(r.LeaseId) && r.LeaseStartUtc <= now && now < r.LeaseEndUtc, "ACTIVE_LEASE_REQUIRED");
        Require(r.AuditFaultStatus is 500 or 503 && !string.IsNullOrWhiteSpace(r.AuditFaultApproval), "AUDIT_FAULT_CONTRACT_REQUIRED");
        Require(r.ActorId > 0 && r.DeniedActorId > 0 && r.ActorId != r.DeniedActorId && r.ActorUsername.Length > 0 && r.DeniedUsername.Length > 0 && r.ActorUsername != r.DeniedUsername, "DEDICATED_IDENTITIES_REQUIRED");
        Require(r.CodePrefix.Length is >= 4 and <= 12 && r.CodePrefix.All(char.IsAsciiLetterOrDigit), "OWNED_CODE_PREFIX_REQUIRED");
        Require(r.AllowedMutations.Order().SequenceEqual(Mutations.Order()), "EXACT_MUTATION_ALLOWLIST_REQUIRED");
        Require(Uri.TryCreate(r.Origin, UriKind.Absolute, out var u) && u.Scheme == "https" && u.Host == "localhost" && !u.IsDefaultPort && u.AbsolutePath == "/" && u.Query == "" && u.Fragment == "" && u.UserInfo == "", "LOOPBACK_HTTPS_REQUIRED");
    }
    public static SqlConnectionStringBuilder Connection(TrialReceipt r, string connection)
    {
        var c = new SqlConnectionStringBuilder(connection);
        Require(c.DataSource.Equals(r.Server, StringComparison.OrdinalIgnoreCase) && c.InitialCatalog == r.Database && !c.TrustServerCertificate
            && c.Encrypt.ToString() is "True" or "Mandatory" or "Strict" && c.AttachDBFilename == "" && c.FailoverPartner == ""
            && c.ApplicationIntent == ApplicationIntent.ReadWrite, "VERIFIED_TLS_EXACT_SQL_REQUIRED");
        Require(string.IsNullOrEmpty(c.HostNameInCertificate) && string.IsNullOrEmpty(c.ServerCertificate), "SQL_HOSTNAME_CHAIN_OVERRIDE_NOT_ALLOWED");
        return c;
    }
}
public sealed class GuardFailure(string code) : Exception(code);
