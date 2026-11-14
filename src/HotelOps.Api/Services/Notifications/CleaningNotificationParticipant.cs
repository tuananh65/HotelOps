using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Data;

namespace HotelOps.Api.Services.Notifications;
public sealed class CleaningAssignmentLease
{
    internal CleaningAssignmentLease(Guid tx,CleaningAssignmentCommand command,CleaningAssignmentReceipt? replay)
        =>(TransactionId,Command,Replay)=(tx,command,replay);
    internal Guid TransactionId{get;}internal CleaningAssignmentCommand Command{get;}
    internal string? Proof{get;set;}internal bool Armed{get;set;}=true;
    public CleaningAssignmentReceipt? Replay{get;}
}
// Mandatory participation: Enter BEFORE mutation/native module, Stage/Adopt BEFORE EF commit.
public sealed class CleaningNotificationParticipant(HotelOpsDbContext db,NotificationCommitGuard guard,
    ICleaningNotificationActivation activation,ICleaningActorAuthority authority,ICleaningAssignmentStore store,TimeProvider clock)
{
    private readonly HashSet<CleaningAssignmentLease> leases=[];
    public async Task<CleaningAssignmentLease> EnterAsync(DbConnection connection,DbTransaction transaction,
        CleaningAssignmentCommand command,CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();var tx=Fe05CallerTransaction.Require(db,connection,transaction);guard.RequireAttached(db,tx);
            CleaningAssignmentProfile.ValidateCommand(command);
            await activation.RequireAsync(CleaningNotificationCapability.Staging,ct);
            await store.EnsureAsync(ct);await authority.RequireWriterAsync(command,ct);
            // Retained key range; no row insert here: fs013_assign_with_evidence rejects preinserted reservations.
            var op=await store.LockOperationAsync(command.ActorId,command.OperationId,ct);
            CleaningAssignmentReceipt? replay=null;
            if(op is not null)
            {
                if(!StringComparer.OrdinalIgnoreCase.Equals(op.Fingerprint,command.RequestFingerprint))throw new NotificationConflictException();
                if(!op.Completed||op.ResultJson is null)throw new NotificationContractUnavailableException();
                var graph=await store.GraphAsync(command.ActorId,command.OperationId,ct);
                _=CleaningAssignmentProfile.Verify(graph,command);
                replay=JsonSerializer.Deserialize<CleaningAssignmentReceipt>(op.ResultJson,CleaningAssignmentProfile.Json)??throw new NotificationConflictException();
            }
            var lease=new CleaningAssignmentLease(tx,command,replay);leases.Add(lease);
            if(replay is not null)lease.Proof=CleaningAssignmentProfile.PayloadHash(CleaningAssignmentProfile.Verify(await store.GraphAsync(command.ActorId,command.OperationId,ct),command));
            // A second enter/replay in this same transaction must not replace the first
            // fresh-mutation verifier and accidentally downgrade it to historical replay.
            guard.BindGraph(db,tx,"CLEANING:"+command.ActorId+":"+command.OperationId+":"+Guid.NewGuid(),async token=>{
                try
                {if(lease.Proof is null)throw new InvalidOperationException("MANDATORY_CLEANING_STAGING_REQUIRED");
                 await VerifyAsync(lease,lease.Replay is null,token);}
                catch{Poison();throw;}
            });
            return lease;
        }
        catch{Poison();throw;}
    }
    public async Task<CleaningAssignmentReceipt> StageAsync(DbConnection connection,DbTransaction transaction,
        CleaningAssignmentLease lease,long persistedAssignmentId,CleaningAssignmentReceipt ownerReceipt,CancellationToken ct)
    {
        try
        {
            RequireLease(connection,transaction,lease);ct.ThrowIfCancellationRequested();
            if(lease.Replay is not null||!lease.Armed)throw new NotificationConflictException();
            await authority.RequireWriterAsync(lease.Command,ct);
            var s=await store.SourceAsync(persistedAssignmentId,ct)??throw new CleaningSourceStaleException();
            var payload=CleaningAssignmentProfile.Build(lease.Command,s,ownerReceipt,clock.GetUtcNow().UtcDateTime);
            if(!await store.CanReadAsync(s,s.AssigneeId,ct))throw new UnauthorizedAccessException();
            var existing=await store.LockOperationAsync(lease.Command.ActorId,lease.Command.OperationId,ct);
            if(existing is null)await store.InsertAsync(lease.Command,payload,ownerReceipt,ct);
            // Native SQL boundary already created exactly this graph: adopt/verify, never insert again.
            var graph=await store.GraphAsync(lease.Command.ActorId,lease.Command.OperationId,ct);
            var observed=CleaningAssignmentProfile.Verify(graph,lease.Command);
            if(CleaningAssignmentProfile.PayloadHash(observed)!=CleaningAssignmentProfile.PayloadHash(payload)||
                JsonSerializer.Serialize(ownerReceipt,CleaningAssignmentProfile.Json)!=graph.ResultJson)
            {
                // Native SQL JSON formatting can differ; compare the typed owner receipt, not whitespace.
                var stored=JsonSerializer.Deserialize<CleaningAssignmentReceipt>(graph.ResultJson,CleaningAssignmentProfile.Json);
                if(stored!=ownerReceipt||observed!=payload)throw new NotificationConflictException();
            }
            lease.Proof=CleaningAssignmentProfile.PayloadHash(observed);lease.Armed=false;
            await VerifyAsync(lease,true,ct);return ownerReceipt;
        }
        catch{Poison();throw;}
    }
    private async Task VerifyAsync(CleaningAssignmentLease lease,bool current,CancellationToken ct)
    {
        await activation.RequireAsync(CleaningNotificationCapability.Staging,ct);
        await authority.RequireWriterAsync(lease.Command,ct);
        var g=await store.GraphAsync(lease.Command.ActorId,lease.Command.OperationId,ct);
        var e=CleaningAssignmentProfile.Verify(g,lease.Command);
        if(CleaningAssignmentProfile.PayloadHash(e)!=lease.Proof)throw new NotificationConflictException();
        if(current)
        {
            var s=await store.SourceAsync(e.AssignmentId,ct)??throw new CleaningSourceStaleException();
            CleaningAssignmentProfile.VerifySource(e,s,true);
            if(!await store.CanReadAsync(s,s.AssigneeId,ct))throw new UnauthorizedAccessException();
        }
    }
    private void RequireLease(DbConnection c,DbTransaction t,CleaningAssignmentLease lease)
    {var tx=Fe05CallerTransaction.Require(db,c,t);guard.RequireAttached(db,tx);if(!leases.Contains(lease)||lease.TransactionId!=tx)throw new InvalidOperationException("Exact Cleaning participant lease required.");}
    private void Poison(){if(db.Database.CurrentTransaction is {} tx)guard.Fail(db,tx.TransactionId);}
}
