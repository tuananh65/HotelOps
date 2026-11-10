using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HotelOps.Api.Services.Notifications;

// Versioned review candidate. No lease issuer, keys, DI registration or runtime activation.
public sealed record NotifWriter004BLease(string Version,string ApprovalReference,string PolicyVersion,
    string Server,string Database,Guid ActualDatabaseGuid,string Login,string DatabaseUser,string SidHex,string Authentication,
    string Module,string ExecutionContext,Guid LeaseId,Guid RetiredLeaseId,string Issuer,
    DateTimeOffset IssuedAt,DateTimeOffset ValidFrom,DateTimeOffset ExpiresAt,DateTimeOffset OwnerHardExpiry,
    long LeaseEpoch,string SourceSha256,string ScriptSha256,string PermissionScopeSha256,string CatalogSha256,
    string OriginalWriterSha256,Dictionary<string,string> ModuleSha256,string[] Uses,int ObservationMaxAgeSeconds);
public sealed record NotifOwner007Signature(string Owner,string SignatureBase64);
public sealed record NotifOwner007SignedLease(string Payload,NotifOwner007Signature[] Signatures);

// Same independently pinned owner-key/RSA-PSS architecture as FE04 acceptance.
// Implementations must resolve protected accepted trust policy, never packet/config-supplied keys.
public interface INotifOwner007KeyPolicy { RSA? GetAcceptedKey(string owner); }
public sealed class ClosedNotifOwner007Keys:INotifOwner007KeyPolicy { public RSA? GetAcceptedKey(string owner)=>null; }
public sealed class VerifiedNotifWriter004BLease
{
    private readonly NotifWriter004BLease snapshot;
    internal VerifiedNotifWriter004BLease(NotifWriter004BLease c){snapshot=c with{ModuleSha256=new(c.ModuleSha256,StringComparer.Ordinal),Uses=c.Uses.ToArray()};}
    public NotifWriter004BLease Contract=>snapshot with{ModuleSha256=new(snapshot.ModuleSha256,StringComparer.Ordinal),Uses=snapshot.Uses.ToArray()};
}
public static class NotifWriter004BContract
{
    public const string Version="WRITER004B_OWNER007_V1";
    public const string PolicyVersion="FE04_OWNER007_EXACT_TARGET_V1";
    public const string OriginalWriterSha256="B09D16168882F4D3D9EF1C6A065771552B04B4CA07CAA3734645753B58B434BF";
    private static readonly string[] Owners=["ProjectLeader","SecurityOwner","NotificationOwner","SchemaOwner","RuntimeOwner"];
    private static readonly string[] Uses=["NATIVE_RECEIPT_VERIFY","CONTAINED_DELIVERY","INBOX_READ","INBOX_WRITE"];
    public static VerifiedNotifWriter004BLease Verify(NotifOwner007SignedLease packet,INotifOwner007KeyPolicy keys,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(packet.Payload)||packet.Signatures is null||packet.Signatures.Length!=Owners.Length)
            throw Fail("SIGNED_OWNER_LEASE_REQUIRED");
        foreach(var owner in Owners)
        {
            using var key=keys.GetAcceptedKey(owner)??throw Fail("ACCEPTED_OWNER_KEY_REQUIRED");
            var s=packet.Signatures.Where(s=>s.Owner==owner).ToArray();
            if(key.KeySize<3072||s.Length!=1)throw Fail("EXACT_OWNER_SIGNATURE_REQUIRED");
            byte[] signature;try{signature=Convert.FromBase64String(s[0].SignatureBase64);}catch(FormatException){throw Fail("OWNER_SIGNATURE_INVALID");}
            if(!key.VerifyData(Encoding.UTF8.GetBytes(Version+"\n"+packet.Payload),signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw Fail("OWNER_SIGNATURE_INVALID");
        }
        NotifWriter004BLease c;
        try{c=JsonSerializer.Deserialize<NotifWriter004BLease>(packet.Payload)??throw Fail("LEASE_CONTRACT_REQUIRED");}
        catch(JsonException){throw Fail("LEASE_CONTRACT_REQUIRED");}
        RequireShape(c,now);
        return new(c);
    }
    public static void RequireShape(NotifWriter004BLease c,DateTimeOffset now)
    {
        if(c.Version!=Version||c.PolicyVersion!=PolicyVersion||string.IsNullOrWhiteSpace(c.ApprovalReference)||c.Issuer!="RuntimeOwner")throw Fail("VERSION_OWNER_POLICY_APPROVAL_REQUIRED");
        // This successor does not move the approved common target or rename its principal.
        if(c.Server!=NotifWriter004AContract.Server||c.Database!=NotifWriter004AContract.Database||c.ActualDatabaseGuid!=NotifWriter004AContract.DatabaseGuid)
            throw Fail("EXACT_COMMON_TARGET_ACTUAL_GUID_REQUIRED");
        if(c.Login!="HotelOps_DEV2_FullStack_Runtime"||c.DatabaseUser!=c.Login||c.Authentication!="SQL_LOGIN"||
            c.SidHex is null||c.SidHex.Length<32||c.SidHex.Length%2!=0||!c.SidHex.All(Uri.IsHexDigit))throw Fail("APPROVED_PRINCIPAL_SID_REQUIRED");
        if(c.Module!="dbo.fs013_assign_with_evidence"||c.ExecutionContext!="CALLER_STATIC_DBO_CHAIN"||c.ModuleSha256 is null||
            !c.ModuleSha256.ContainsKey(c.Module)||!c.ModuleSha256.ContainsKey("dbo.fs009_verify_assignment_guard")||c.ModuleSha256.Count!=2||c.ModuleSha256.Values.Any(h=>!Hash(h)))throw Fail("EXACT_NATIVE_MODULE_SCOPE_REQUIRED");
        if(c.LeaseId==Guid.Empty||c.RetiredLeaseId==Guid.Empty||c.LeaseId==c.RetiredLeaseId||c.LeaseEpoch<=0||c.IssuedAt>now||c.ValidFrom<c.IssuedAt||
            c.ValidFrom>now||c.ExpiresAt<=now||c.ExpiresAt<=c.ValidFrom||c.ExpiresAt>c.OwnerHardExpiry||c.OwnerHardExpiry<=now)
            throw Fail("FRESH_INDEPENDENT_OWNER_LEASE_REQUIRED");
        if(c.OriginalWriterSha256!=OriginalWriterSha256||!Hash(c.SourceSha256)||!Hash(c.ScriptSha256)||!Hash(c.PermissionScopeSha256)||!Hash(c.CatalogSha256))throw Fail("EXACT_SOURCE_SCRIPT_POLICY_HASHES_REQUIRED");
        if(c.Uses is null||c.Uses.Length==0||c.Uses.Distinct(StringComparer.Ordinal).Count()!=c.Uses.Length||c.Uses.Any(u=>!Uses.Contains(u,StringComparer.Ordinal))||c.ObservationMaxAgeSeconds is <1 or >60)
            throw Fail("EXACT_OWNER_APPROVED_USE_SCOPE_REQUIRED");
    }
    internal static bool Hash(string? s)=>s is {Length:64}&&s.All(Uri.IsHexDigit);
    internal static InvalidOperationException Fail(string code)=>new(code);
}
