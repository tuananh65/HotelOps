using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;

namespace HotelOps.Api.Services.Notifications;

// Candidate protocol extending FS013 only. Not registered as a producer/publisher or HTTP API.
// RequestFingerprint must hash the complete DEV1 canonical mutation request, including expected
// revision and requested assignee/defects/due date. It is not a post-mutation event fingerprint.
public sealed record Fe05OperationRequest(Guid OperationId, long BranchId, long InspectionId,
    string RequestFingerprint, IReadOnlyList<Fe05NotificationKind> RequiredEvents);
public sealed record Fe05PublicationReceipt(Guid PublicationId, string EventType, short Version,
    string Fingerprint, IReadOnlyList<long> RecipientIds);
public sealed record Fe05OperationReceipt(string Contract, long ActorId, Guid OperationId,
    string RequestFingerprint, IReadOnlyList<Fe05PublicationReceipt> Publications)
{
    public const string ContractName = "FE05_N01_FS013_CANDIDATE_V1";
    // A receipt read inside this transaction cannot attest that the caller committed it.
    public string Durability => "CALLER_TRANSACTION_NOT_COMMITTED";
}
public sealed class Fe05OperationLease
{
    internal Fe05OperationLease(Guid tx, long actor, Fe05OperationRequest request,
        string key, Fe05OperationReceipt? replay) => (TransactionId,ActorId,Request,LedgerFingerprint,Replay)=(tx,actor,request,key,replay);
    internal Guid TransactionId {get;}
    internal long ActorId {get;}
    internal Fe05OperationRequest Request {get;}
    internal string LedgerFingerprint {get;}
    internal IReadOnlyList<(Fe05NotificationCommand Command, Fe05NotificationCandidate Candidate)>? StageProof {get;set;}
    internal Fe05OperationReceipt? StagedReceipt {get;set;}
    public Fe05OperationReceipt? Replay {get;}
}

public static class Fe05OperationIdentity
{
    public static string Fingerprint(Fe05OperationRequest request)
    {
        if(request.OperationId==Guid.Empty || request.BranchId<=0 || request.InspectionId<=0 ||
            request.RequestFingerprint.Length!=64 || !request.RequestFingerprint.All(Uri.IsHexDigit) ||
            request.RequiredEvents.Count is <1 or >2 || request.RequiredEvents.Any(x=>!Enum.IsDefined(x)) ||
            request.RequiredEvents.Distinct().Count()!=request.RequiredEvents.Count)
            throw new Fe05NotificationUnavailableException("FE05_INVALID_OPERATION");
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            contract=Fe05OperationReceipt.ContractName,request.OperationId,request.BranchId,request.InspectionId,
            requestFingerprint=request.RequestFingerprint.ToUpperInvariant(),events=request.RequiredEvents.Order().ToArray()})));
    }
    public static void RequireReplay(Fe05OperationReceipt receipt, long actor, Fe05OperationRequest request)
    {
        if(receipt.Contract!=Fe05OperationReceipt.ContractName || receipt.ActorId!=actor || receipt.OperationId!=request.OperationId ||
            !StringComparer.OrdinalIgnoreCase.Equals(receipt.RequestFingerprint,request.RequestFingerprint) ||
            receipt.Publications.Count!=request.RequiredEvents.Count ||
            receipt.Publications.Any(p=>p.PublicationId==Guid.Empty || p.Version!=1 || p.Fingerprint.Length!=64 ||
                !p.Fingerprint.All(Uri.IsHexDigit) || p.RecipientIds.Count is <1 or >200 || p.RecipientIds.Any(x=>x<=0) ||
                !p.RecipientIds.SequenceEqual(p.RecipientIds.Distinct().Order())) ||
            receipt.Publications.Select(p=>p.PublicationId).Distinct().Count()!=receipt.Publications.Count ||
            !receipt.Publications.Select(p=>p.EventType).Order().SequenceEqual(request.RequiredEvents.Select(EventType).Order()))
            throw new NotificationConflictException();
    }
    public static string EventType(Fe05NotificationKind kind)=>kind switch {
        Fe05NotificationKind.InspectionFailed=>"inspection.failed",
        Fe05NotificationKind.CleaningReworkRequested=>"cleaning.rework.requested",
        _=>throw new Fe05NotificationUnavailableException("FE05_INVALID_OPERATION")};
}

public sealed record Fe05StoredOperation(string Fingerprint, string DomainFingerprint, bool Completed,
    string? DomainResultJson, string? N01ReceiptJson);
public interface IFe05OperationStore
{
    // All calls retain the same transaction. Implementations may not commit/rollback it.
    Task RequireAvailableAsync(CancellationToken ct);
    Task<Fe05StoredOperation?> LockAsync(long actor, Guid operation, CancellationToken ct);
    Task ReserveAsync(long actor, Guid operation, string fingerprint, string domainFingerprint, CancellationToken ct);
    Task WritePublicationAsync(Fe05NotificationCandidate candidate, Guid publication, CancellationToken ct);
    Task CompleteAsync(long actor, Guid operation, string fingerprint, Fe05OperationReceipt receipt, CancellationToken ct);
    Task VerifyAsync(long actor, Guid operation, string fingerprint, Fe05OperationReceipt receipt, CancellationToken ct);
}

public sealed class Fe05OperationStaging(HotelOpsDbContext db, NotificationCommitGuard guard,
    ICurrentUserAccessor current, IFe05SourceCoordination coordination, Fe05NotificationStaging preparation,
    IFe05OperationStore store)
{
    private readonly HashSet<Fe05OperationLease> leases=[];
    public async Task<Fe05OperationLease> ReserveAsync(DbConnection connection, DbTransaction transaction,
        Fe05OperationRequest request, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var tx=Fe05CallerTransaction.Require(db,connection,transaction); guard.RequireAttached(db,tx);
            var fingerprint=Fe05OperationIdentity.Fingerprint(request);
            request=request with {RequiredEvents=Array.AsReadOnly(request.RequiredEvents.Order().ToArray())};
            var actor=Actor();
            // Must precede domain mutation, after owner Users/session/branch/catalog locks.
            await coordination.RequireReservationAsync(db,request,actor,ct);
            await store.RequireAvailableAsync(ct);
            var stored=await store.LockAsync(actor.UserId,request.OperationId,ct);
            Fe05OperationReceipt? replay=null;
            if(stored is not null)
            {
                if(stored.Fingerprint!=fingerprint || !StringComparer.OrdinalIgnoreCase.Equals(stored.DomainFingerprint,request.RequestFingerprint))throw new NotificationConflictException();
                if(!stored.Completed || stored.DomainResultJson is null || stored.N01ReceiptJson is null)
                    throw new Fe05NotificationUnavailableException("FE05_INCOMPLETE_OPERATION_ROLLBACK_REQUIRED");
                replay=JsonSerializer.Deserialize<Fe05OperationReceipt>(stored.N01ReceiptJson)
                    ?? throw new NotificationConflictException();
                Fe05OperationIdentity.RequireReplay(replay,actor.UserId,request);
                replay=replay with {Publications=Array.AsReadOnly(replay.Publications.Select(p=>p with {
                    RecipientIds=Array.AsReadOnly(p.RecipientIds.ToArray())}).ToArray())};
                await store.VerifyAsync(actor.UserId,request.OperationId,fingerprint,replay,ct);
                await coordination.RequireCompletedOperationAsync(db,request,actor,replay,ct);
            }
            else await store.ReserveAsync(actor.UserId,request.OperationId,fingerprint,request.RequestFingerprint,ct);
            // Freeze lists so caller mutation cannot retarget the reserved event set.
            var frozen=request with {RequiredEvents=Array.AsReadOnly(request.RequiredEvents.Order().ToArray())};
            var lease=new Fe05OperationLease(tx,actor.UserId,frozen,fingerprint,replay); leases.Add(lease);
            guard.BindGraph(db,tx,"N01:"+actor.UserId+":"+request.OperationId,async token=>{
                var saved=await store.LockAsync(actor.UserId,request.OperationId,token);
                if(saved is not {Completed:true,DomainResultJson:not null,N01ReceiptJson:not null} || saved.Fingerprint!=fingerprint)
                    throw new InvalidOperationException("FE05_REQUIRED_STAGING_INCOMPLETE");
                var receipt=JsonSerializer.Deserialize<Fe05OperationReceipt>(saved.N01ReceiptJson) ?? throw new NotificationConflictException();
                Fe05OperationIdentity.RequireReplay(receipt,actor.UserId,frozen);
                var expected=lease.Replay ?? lease.StagedReceipt ?? throw new InvalidOperationException("FE05_REQUIRED_STAGING_INCOMPLETE");
                if(saved.N01ReceiptJson!=JsonSerializer.Serialize(expected))throw new NotificationConflictException();
                await store.VerifyAsync(actor.UserId,request.OperationId,fingerprint,receipt,token);
                var currentActor=Actor();
                if(currentActor.UserId!=actor.UserId)throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
                await coordination.RequireCompletedOperationAsync(db,frozen,currentActor,receipt,token);
                if(lease.Replay is null)
                {
                    if(lease.StageProof is null)throw new InvalidOperationException("FE05_REQUIRED_STAGING_INCOMPLETE");
                    foreach(var proof in lease.StageProof)
                    {
                        var observed=await preparation.PrepareAsync(proof.Command,token);
                        if(observed.Fingerprint!=proof.Candidate.Fingerprint)throw new NotificationConflictException();
                    }
                }
            });
            return lease;
        }
        catch {Poison();throw;}
    }

    public async Task<Fe05OperationReceipt> StageRequiredAsync(DbConnection connection, DbTransaction transaction,
        Fe05OperationLease lease, IReadOnlyList<Fe05NotificationCommand> commands, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var tx=Fe05CallerTransaction.Require(db,connection,transaction); guard.RequireAttached(db,tx);
            if(!leases.Contains(lease) || lease.TransactionId!=tx || Actor().UserId!=lease.ActorId || lease.Replay is not null)
                throw new InvalidOperationException("FE05_MATCHING_NEW_OPERATION_LEASE_REQUIRED");
            var request=lease.Request;
            commands=commands.ToArray();
            if(commands.Count!=request.RequiredEvents.Count ||
                commands.Any(c=>c.OperationId!=request.OperationId || c.BranchId!=request.BranchId || c.InspectionId!=request.InspectionId) ||
                !commands.Select(c=>c.Kind).Order().SequenceEqual(request.RequiredEvents))
                throw new NotificationConflictException();
            var candidates=new List<Fe05NotificationCandidate>();
            foreach(var command in commands.OrderBy(x=>x.Kind))candidates.Add(await preparation.PrepareAsync(command,ct));
            // Validate every mandatory source and recipient before the first publication insert.
            var receipts=new List<Fe05PublicationReceipt>();
            foreach(var candidate in candidates)
            {
                var publication=Guid.NewGuid(); // durable graph identity; never a domain identifier
                await store.WritePublicationAsync(candidate,publication,ct);
                receipts.Add(new(publication,candidate.EventType,candidate.Version,candidate.Fingerprint,candidate.RecipientIds));
            }
            var receipt=new Fe05OperationReceipt(Fe05OperationReceipt.ContractName,lease.ActorId,request.OperationId,
                request.RequestFingerprint.ToUpperInvariant(),receipts.AsReadOnly());
            await store.CompleteAsync(lease.ActorId,request.OperationId,lease.LedgerFingerprint,receipt,ct);
            await store.VerifyAsync(lease.ActorId,request.OperationId,lease.LedgerFingerprint,receipt,ct);
            lease.StageProof=commands.OrderBy(x=>x.Kind).Zip(candidates,(command,candidate)=>(command,candidate)).ToArray();
            lease.StagedReceipt=receipt;
            leases.Remove(lease);
            return receipt;
        }
        catch {Poison();throw;}
    }
    private CurrentUserInfo Actor()=>current.TryGetCurrentUser(out var actor) && actor.UserId>0 && actor.SessionId>0 && !actor.IsRestricted
        ? actor : throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
    private void Poison(){if(db.Database.CurrentTransaction is {} tx)guard.Fail(db,tx.TransactionId);}
}
