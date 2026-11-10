namespace HotelOps.Api.Services.Notifications;

// Proposed preflight contract only. Not registered in DI or used to widen existing guards.
// Approval and observation acquisition remain owner responsibilities; this is not an issuer.
public sealed record NotifWriter004AObservation(string Server,string Database,Guid DatabaseGuid,
    string Owner,string LeaseId,DateTimeOffset LeaseExpiresUtc,bool CentralLeaseHeld,
    string Login,string DatabaseUser,string Authentication,string Module,string ExecutionContext,
    int TransactionCount,bool CallerOwnsTransaction,bool Serializable,bool SessionAndSourceVerified,
    bool IsPrivileged,bool DirectGraphWritesDenied);

public sealed record NotifWriter004AOwnerContract(string ApprovalId,string Login,string DatabaseUser,
    string Authentication,string Module,string ExecutionContext,string LeaseId);

public static class NotifWriter004AContract
{
    public const string Server="DESKTOP-JI8U50T\\SQLEXPRESS";
    public const string Database="HotelOps_ENVNOTIF001_Trial_20261109";
    public static readonly Guid DatabaseGuid=Guid.Parse("a138fa48-cc14-4070-9ca4-793f1b60c5c1");
    public const string Owner="DEV1 ENV-001 / DEV2";
    public static readonly DateTimeOffset HardExpiry=DateTimeOffset.Parse("2026-11-10T11:44:09.1792768Z");

    // No accepted production contract is shipped. A caller-supplied record alone is NOT authorization.
    // Only an owner-accepted adapter may supply observations and approval, after its own trust checks.
    public static void Require(NotifWriter004AObservation o,NotifWriter004AOwnerContract? accepted,DateTimeOffset now)
    {
        if(accepted is null||new[]{accepted.ApprovalId,accepted.Login,accepted.DatabaseUser,accepted.Authentication,
            accepted.Module,accepted.ExecutionContext,accepted.LeaseId}.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("WRITER_AUTH_MAPPING_MODULE_OWNER_CONTRACT_BLOCKED");
        if(o.Server!=Server||o.Database!=Database||o.DatabaseGuid!=DatabaseGuid||o.Owner!=Owner)
            throw new InvalidOperationException("EXACT_ENV001_TARGET_GUID_OWNER_REQUIRED");
        if(!o.CentralLeaseHeld||o.LeaseId!=accepted.LeaseId||o.LeaseExpiresUtc>HardExpiry||
            o.LeaseExpiresUtc<=now||now>=HardExpiry)
            throw new InvalidOperationException("CURRENT_CENTRAL_LEASE_REQUIRED");
        if(o.IsPrivileged||o.Login.Equals("hax",StringComparison.OrdinalIgnoreCase)||
            o.Login.EndsWith("\\hax",StringComparison.OrdinalIgnoreCase)||!o.DirectGraphWritesDenied||
            o.Login!=accepted.Login||o.DatabaseUser!=accepted.DatabaseUser||o.Authentication!=accepted.Authentication)
            throw new InvalidOperationException("RESTRICTED_MAPPED_WRITER_REQUIRED");
        if(o.Module!=accepted.Module||o.Module=="dbo.fs013_unapproved_writer_probe"||
            o.ExecutionContext!=accepted.ExecutionContext||!o.SessionAndSourceVerified)
            throw new InvalidOperationException("AUTHORIZED_MODULE_SESSION_SOURCE_REQUIRED");
        if(o.TransactionCount!=1||!o.CallerOwnsTransaction||!o.Serializable)
            throw new InvalidOperationException("ONE_CALLER_OWNED_SERIALIZABLE_TRANSACTION_REQUIRED");
    }
}
