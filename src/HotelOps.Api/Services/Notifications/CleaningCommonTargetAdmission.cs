using System.Data;
using System.Data.Common;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

public interface ICleaningCommonTargetAdmission
{
    Task RequireStoreAsync(HotelOpsDbContext db,CancellationToken ct);
    Task RequireRuntimeCleanAsync(HotelOpsDbContext db,string use,CancellationToken ct);
}
public sealed record NotifOwner007Observation(DbConnection Connection,DbTransaction? Transaction,
    string Server,string Database,Guid? ActualDatabaseGuid,string GuidSource,bool MetadataVisible,
    DateTimeOffset ObservedAt,string Login,string DatabaseUser,string SidHex,bool Restricted,bool DirectGraphWritesDenied,
    string SourceSha256,string ScriptSha256,string PermissionScopeSha256,string CatalogSha256,
    bool NativeModuleHashesMatch,bool Fs013ProposalMarker,bool OperationUniqueIndex,bool IntentUniqueIndex,
    bool PublicationUniqueIndex,bool OperationImmutableTrigger,bool PublicationImmutableTrigger,
    bool SourceAuthorityVerified,string ExpectedSourceRevision,string ActualSourceRevision,int TransactionCount,bool CallerOwnsTransaction);
public interface INotifOwner007RuntimeAuthority
{
    // Accepted adapter must validate REAL current central ownership/revocation, never a local lock/bool.
    Task RequireCurrentLeaseAsync(VerifiedNotifWriter004BLease lease,string use,CancellationToken ct);
    // Same already-authorized open caller connection/transaction only. No Open/alternate target/grants.
    // Must independently observe actual database_guid, principal/SID, effective DENYs, catalog/module
    // hashes and retained owner source/revision authority. No production implementation is shipped.
    Task<NotifOwner007Observation> ObserveExistingAsync(VerifiedNotifWriter004BLease lease,DbConnection connection,DbTransaction? transaction,CancellationToken ct);
}
public sealed class ClosedNotifOwner007RuntimeAuthority:INotifOwner007RuntimeAuthority
{
    public Task RequireCurrentLeaseAsync(VerifiedNotifWriter004BLease lease,string use,CancellationToken ct)=>throw new NotificationContractUnavailableException();
    public Task<NotifOwner007Observation> ObserveExistingAsync(VerifiedNotifWriter004BLease lease,DbConnection connection,DbTransaction? transaction,CancellationToken ct)=>throw new NotificationContractUnavailableException();
}

// Not registered by production composition. FE04 owner supplies accepted adapter and independently
// pinned policy only after D03/D04/D05/D06/D08 decisions. Raw caller assertions are not authority.
public sealed class CleaningCommonTargetAdmission:ICleaningCommonTargetAdmission
{
    private readonly NotifOwner007SignedLease packet;
    private readonly INotifOwner007KeyPolicy keys;
    private readonly INotifOwner007RuntimeAuthority authority;
    private readonly TimeProvider clock;
    public CleaningCommonTargetAdmission(NotifOwner007SignedLease packet,INotifOwner007KeyPolicy keys,INotifOwner007RuntimeAuthority authority,TimeProvider clock)
    {this.packet=packet with{Signatures=packet.Signatures.ToArray()};this.keys=keys;this.authority=authority;this.clock=clock;}
    public Task RequireStoreAsync(HotelOpsDbContext db,CancellationToken ct)
    {ct.ThrowIfCancellationRequested();Fe05CallerTransaction.Require(db);return RequireAsync(db.Database.GetDbConnection(),db.Database.CurrentTransaction!.GetDbTransaction(),"NATIVE_RECEIPT_VERIFY",ct);}
    public Task RequireRuntimeCleanAsync(HotelOpsDbContext db,string use,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(use is not("CONTAINED_DELIVERY" or "INBOX_READ" or "INBOX_WRITE")||db.Database.CurrentTransaction is not null||db.ChangeTracker.Entries().Any())throw new NotificationContractUnavailableException();
        return RequireAsync(db.Database.GetDbConnection(),null,use,ct);
    }
    private async Task RequireAsync(DbConnection connection,DbTransaction? transaction,string use,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();var verified=NotifWriter004BContract.Verify(packet,keys,clock.GetUtcNow());var c=verified.Contract;
        if(!c.Uses.Contains(use,StringComparer.Ordinal)||connection.State!=ConnectionState.Open||connection.DataSource!=c.Server||connection.Database!=c.Database)
            throw new NotificationContractUnavailableException();
        if(transaction is not null&&(!ReferenceEquals(transaction.Connection,connection)||transaction.IsolationLevel!=IsolationLevel.Serializable))throw new NotificationContractUnavailableException();
        await authority.RequireCurrentLeaseAsync(verified,use,ct);
        var actual=await authority.ObserveExistingAsync(verified,connection,transaction,ct);
        RequireObservation(c,actual,connection,transaction,clock.GetUtcNow());
        // Recheck expiry/current ownership AFTER metadata observation; never use a retained old grant.
        NotifWriter004BContract.RequireShape(c,clock.GetUtcNow());await authority.RequireCurrentLeaseAsync(verified,use,ct);
    }
    public static void RequireObservation(NotifWriter004BLease c,NotifOwner007Observation o,DbConnection connection,DbTransaction? transaction,DateTimeOffset now)
    {
        if(!ReferenceEquals(o.Connection,connection)||!ReferenceEquals(o.Transaction,transaction)||connection.State!=ConnectionState.Open||
            connection.DataSource!=c.Server||connection.Database!=c.Database||o.Server!=c.Server||o.Database!=c.Database||!o.MetadataVisible||o.GuidSource!="sys.database_recovery_status.database_guid"||
            o.ActualDatabaseGuid is null||o.ActualDatabaseGuid!=c.ActualDatabaseGuid||o.ObservedAt>now||now-o.ObservedAt>TimeSpan.FromSeconds(c.ObservationMaxAgeSeconds))
            throw new NotificationContractUnavailableException();
        if(o.Login!=c.Login||o.DatabaseUser!=c.DatabaseUser||o.SidHex!=c.SidHex||!o.Restricted||!o.DirectGraphWritesDenied||
            o.SourceSha256!=c.SourceSha256||o.ScriptSha256!=c.ScriptSha256||o.PermissionScopeSha256!=c.PermissionScopeSha256||o.CatalogSha256!=c.CatalogSha256||
            !o.NativeModuleHashesMatch||!o.Fs013ProposalMarker||!o.OperationUniqueIndex||!o.IntentUniqueIndex||!o.PublicationUniqueIndex||!o.OperationImmutableTrigger||!o.PublicationImmutableTrigger)
            throw new NotificationContractUnavailableException();
        if(transaction is null){if(o.TransactionCount!=0)throw new NotificationContractUnavailableException();}
        else if(!ReferenceEquals(transaction.Connection,connection)||transaction.IsolationLevel!=IsolationLevel.Serializable||o.TransactionCount!=1||!o.CallerOwnsTransaction||
            !o.SourceAuthorityVerified||string.IsNullOrEmpty(o.ExpectedSourceRevision)||o.ActualSourceRevision!=o.ExpectedSourceRevision)
            throw new NotificationContractUnavailableException();
    }
}
