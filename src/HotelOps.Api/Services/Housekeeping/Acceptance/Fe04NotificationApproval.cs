using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Services.Notifications;

namespace HotelOps.Api.Services.Housekeeping.Acceptance;

// Acceptance-host only. No production registration, permission issuer or connection fallback.
public sealed record Fe04Permission(string Object, string Permission, string? Column, bool Allowed);
public sealed record Fe04NotificationTrialContract(
    string Version, string Server, string Database, Guid DatabaseGuid, string Owner,
    string Login, string DatabaseUser, string Authentication, string Module, string ExecutionContext,
    Guid LeaseId, DateTimeOffset LeaseStart, DateTimeOffset LeaseEnd,
    string SourceCommit, string AssemblySha256, string FixtureManifestSha256,
    Dictionary<string, string> Files, Dictionary<string, string> SqlModules,
    Fe04Permission[] Permissions, string CatalogSha256, string FixtureOwner,
    string[] MutationCases, Dictionary<string, string> Decisions);
public sealed record Fe04OwnerSignature(string Owner, string PublicKeyPem, string SignatureBase64);
public sealed record Fe04SignedApproval(string Payload, Fe04OwnerSignature[] Signatures);

public static class Fe04NotificationApproval
{
    public const string Version = "FE04_NOTIFICATION_ACCEPTANCE_V1";
    public const string Login = "HotelOps_DEV2_FullStack_Runtime";
    public static readonly string[] Owners = ["ProjectLeader", "SecurityOwner", "NotificationOwner", "SchemaOwner", "RuntimeOwner"];
    public static readonly string[] Decisions = ["authentication", "mapping", "permissions", "commonTarget", "schema", "fixtures", "lease", "mutationScope"];
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));

    // Pins come from an owner-installed OS-protected trust policy, NEVER from the approval packet.
    public static Fe04NotificationTrialContract Verify(Fe04SignedApproval packet,
        IReadOnlyDictionary<string, string> ownerKeyPins, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(packet.Payload) || packet.Signatures is null)
            throw new InvalidOperationException("OWNER_APPROVAL_REQUIRED");
        foreach (var owner in Owners)
        {
            if (!ownerKeyPins.TryGetValue(owner, out var pin) || !IsHash(pin))
                throw new InvalidOperationException("OWNER_TRUST_POLICY_REQUIRED");
            var signatures = packet.Signatures.Where(s => s.Owner == owner).ToArray();
            if (signatures.Length != 1) throw new InvalidOperationException("EXACT_OWNER_SIGNATURE_REQUIRED");
            using var rsa = RSA.Create();
            rsa.ImportFromPem(signatures[0].PublicKeyPem);
            if (rsa.KeySize < 3072 || Hash(rsa.ExportSubjectPublicKeyInfo()) != pin ||
                !rsa.VerifyData(Encoding.UTF8.GetBytes(Version + "\n" + packet.Payload),
                    Convert.FromBase64String(signatures[0].SignatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new InvalidOperationException("OWNER_SIGNATURE_INVALID");
        }
        var contract = JsonSerializer.Deserialize<Fe04NotificationTrialContract>(packet.Payload)
            ?? throw new InvalidOperationException("CONTRACT_REQUIRED");
        Require(contract, now);
        return contract;
    }

    public static void Require(Fe04NotificationTrialContract c, DateTimeOffset now)
    {
        if (c.Version != Version || c.Server != NotifWriter004AContract.Server ||
            c.Database != NotifWriter004AContract.Database || c.DatabaseGuid != NotifWriter004AContract.DatabaseGuid ||
            c.Owner != NotifWriter004AContract.Owner || c.Login != Login || c.DatabaseUser != Login ||
            c.Authentication != "SQL_LOGIN" || c.Module != "dbo.fs013_assign_with_evidence" ||
            c.ExecutionContext != "CALLER_STATIC_DBO_CHAIN")
            throw new InvalidOperationException("EXACT_TRIAL_IDENTITY_CONTRACT_REQUIRED");
        if (c.LeaseId == Guid.Empty || c.LeaseStart > now || c.LeaseEnd <= now ||
            c.LeaseEnd > NotifWriter004AContract.HardExpiry || c.LeaseEnd <= c.LeaseStart)
            throw new InvalidOperationException("ACTIVE_OWNER_LEASE_REQUIRED_OR_VERSIONED_WRITER_REISSUE");
        if (c.Decisions is null || Decisions.Any(d => !c.Decisions.TryGetValue(d, out var status) || status != "ACCEPTED"))
            throw new InvalidOperationException("SECURITY_SCHEMA_RUNTIME_DECISIONS_NOT_ACCEPTED");
        if (c.SourceCommit is null || c.SourceCommit.Length != 40 || !c.SourceCommit.All(Uri.IsHexDigit) ||
            !IsHash(c.AssemblySha256) || !IsHash(c.FixtureManifestSha256) || !IsHash(c.CatalogSha256) ||
            string.IsNullOrWhiteSpace(c.FixtureOwner) || c.Files is null || c.Files.Count == 0 ||
            c.Files.Any(f => !SafeRelative(f.Key) || !IsHash(f.Value)) || c.SqlModules is null ||
            !c.SqlModules.ContainsKey(c.Module) || !c.SqlModules.ContainsKey("dbo.fs009_verify_assignment_guard") ||
            !c.SqlModules.ContainsKey("dbo.TR_fs009_publication_source") || c.SqlModules.Any(m => !IsHash(m.Value)))
            throw new InvalidOperationException("SOURCE_SCHEMA_FIXTURE_PROVENANCE_REQUIRED");
        if (c.MutationCases is null || !c.MutationCases.Order(StringComparer.Ordinal).SequenceEqual(
            Enumerable.Range(1, 15).Select(i => i.ToString("D2")).Order(StringComparer.Ordinal)))
            throw new InvalidOperationException("EXACT_FIFTEEN_CASE_MUTATION_SCOPE_REQUIRED");
        if (c.Permissions is null || c.Permissions.Length == 0 || c.Permissions.Any(p =>
            string.IsNullOrWhiteSpace(p.Object) || string.IsNullOrWhiteSpace(p.Permission)) ||
            c.Permissions.Select(p => (p.Object, p.Permission, p.Column)).Distinct().Count() != c.Permissions.Length)
            throw new InvalidOperationException("EXACT_PERMISSION_MATRIX_REQUIRED");
        foreach (var table in new[] { "notification_operation_reservations", "notification_publications", "notification_delivery_intents", "cleaning_assignments" })
            foreach (var operation in new[] { "INSERT", "DELETE" }.Concat(table is "notification_operation_reservations" or "notification_publications" or "cleaning_assignments" ? ["UPDATE"] : Array.Empty<string>()))
                if (!c.Permissions.Contains(new("dbo." + table, operation, null, false)))
                    throw new InvalidOperationException("GRAPH_ASSIGNMENT_DENY_BOUNDARY_REQUIRED");
        foreach (var module in new[] { c.Module, "dbo.fs009_verify_assignment_guard" })
            if (!c.Permissions.Contains(new(module, "EXECUTE", null, true)))
                throw new InvalidOperationException("EXACT_NATIVE_MODULE_EXECUTE_REQUIRED");
        if (c.Permissions.Any(p => p.Allowed && p.Permission is "INSERT" or "DELETE" or "UPDATE" &&
            (p.Object is "dbo.notification_operation_reservations" or "dbo.notification_publications" or "dbo.cleaning_assignments" ||
             p.Object == "dbo.notification_delivery_intents" && p.Permission is "INSERT" or "DELETE")))
            throw new InvalidOperationException("NO_DIRECT_GRAPH_OR_ASSIGNMENT_WRITE_GRANT");
    }

    public static async Task VerifyFilesAsync(Fe04NotificationTrialContract c, string root,
        string fixtureManifest, CancellationToken ct)
    {
        var assemblyVersion = typeof(Fe04NotificationApproval).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion;
        if (!assemblyVersion.EndsWith("+" + c.SourceCommit, StringComparison.Ordinal))
            throw new InvalidOperationException("LOADED_ASSEMBLY_SOURCE_COMMIT_MISMATCH");
        var basePath = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        foreach (var file in c.Files)
        {
            var path = Path.GetFullPath(Path.Combine(basePath, file.Key));
            if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) ||
                Hash(await File.ReadAllBytesAsync(path, ct)) != file.Value)
                throw new InvalidOperationException("SOURCE_OR_SCRIPT_HASH_MISMATCH");
        }
        if (Hash(await File.ReadAllBytesAsync(typeof(Fe04NotificationApproval).Assembly.Location, ct)) != c.AssemblySha256 ||
            Hash(await File.ReadAllBytesAsync(fixtureManifest, ct)) != c.FixtureManifestSha256)
            throw new InvalidOperationException("ASSEMBLY_OR_FIXTURE_HASH_MISMATCH");
    }

    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static bool SafeRelative(string path) => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) &&
        !path.Contains(':') && !path.Replace('\\', '/').Split('/').Any(p => p is ".." or "." or "");
}
