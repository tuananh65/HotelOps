using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Safety008;

// Tracks actual generated EF keys from the accepted seed factory; no inferred IDs.
internal sealed class SeedLedger(Snapshot baseline,Durable journal,SqlStore leaseStore,Func<bool> controllerHealthy):SaveChangesInterceptor
{
    internal readonly HashSet<string> Keys=[];
    internal bool CommitIntent {get;private set;}
    internal bool CommitAcknowledged {get;private set;}
    private readonly List<EntityEntry> pending=[];
    internal static string? Canonical(object? value)=>value switch{null=>null,byte[] bytes=>Convert.ToHexString(bytes),Guid g=>g.ToString("D"),_=>Convert.ToString(value,CultureInfo.InvariantCulture)};
    internal static (string Table,SortedDictionary<string,string?> Pk) Key(EntityEntry entry)
    {
        var table=entry.Metadata.GetTableName()??throw new InvalidOperationException("EF_TABLE_REQUIRED");var schema=entry.Metadata.GetSchema()??"dbo";var store=StoreObjectIdentifier.Table(table,entry.Metadata.GetSchema());
        var pk=new SortedDictionary<string,string?>(StringComparer.Ordinal);
        foreach(var property in entry.Metadata.FindPrimaryKey()?.Properties??throw new InvalidOperationException("EF_PK_REQUIRED"))pk.Add(property.GetColumnName(store)!,Canonical(entry.Property(property.Name).CurrentValue));
        return(schema+"."+table,pk);
    }
    internal static string KeyString(EntityEntry e){var k=Key(e);return k.Table+":"+System.Text.Json.JsonSerializer.Serialize(k.Pk);}
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken ct=default)
    {
        var db=eventData.Context??throw new InvalidOperationException("Context required");pending.Clear();
        foreach(var e in db.ChangeTracker.Entries()){
            if(e.State==EntityState.Added)pending.Add(e);
            else if(e.State is EntityState.Modified or EntityState.Deleted){if(!Keys.Contains(KeyString(e)))throw new InvalidOperationException("SEED_ATTEMPTED_TO_CHANGE_UNOWNED_ROW");}
        }
        return ValueTask.FromResult(result);
    }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,int result,CancellationToken ct=default)
    {
        foreach(var entry in pending){var k=Key(entry);var key=KeyString(entry);if(baseline.Rows.ContainsKey(key))throw new InvalidOperationException("GENERATED_KEY_COLLIDES_WITH_PROTECTED_BASELINE");Keys.Add(key);await journal.Event("SEEDED_GENERATED_PK",new{table=k.Table,pk=k.Pk,provenance="ACTUAL_ACCEPTED_FACTORY_EF_ADDED_ENTRY_AFTER_SAVE_BEFORE_COMMIT"});}
        pending.Clear();return result;
    }
    internal DbTransactionInterceptor TransactionGuard()=>new CommitGuard(this,baseline,journal,leaseStore,controllerHealthy);
    private sealed class CommitGuard(SeedLedger owner,Snapshot baseline,Durable journal,SqlStore lease,Func<bool> controllerHealthy):DbTransactionInterceptor
    {
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction tx,TransactionEventData data,InterceptionResult result,CancellationToken ct=default)
        {
            if(!controllerHealthy())throw new InvalidOperationException("CONTROLLER_LOST_ABORT_SEED_COMMIT");
            await lease.RequireOwnLease();var store=new SqlStore((SqlConnection)tx.Connection!,journal);
            var current=await store.Capture((SqlTransaction)tx,true);
            var proof=Graph.Prove(baseline,current,owner.Keys,[],new HashSet<long>());
            if(proof.Problems.Length>0)throw new InvalidOperationException("SEED_PRECOMMIT_OWNERSHIP_OR_PROTECTED_ROW_PROOF_FAILED");
            var again=await store.Capture((SqlTransaction)tx,true);
            if(again.SchemaHash!=current.SchemaHash||again.Rows.Count!=current.Rows.Count||again.Rows.Any(p=>!current.Rows.TryGetValue(p.Key,out var r)||r.Hash!=p.Value.Hash))throw new InvalidOperationException("SEED_PRECOMMIT_RECHECK_CHANGED");
            await journal.Event("SEED_COMMIT_INTENT_PROOF_DURABLE",new{actualPkCount=owner.Keys.Count,protectedRows=baseline.Rows.Count,schemaHash=baseline.SchemaHash});owner.CommitIntent=true;return result;
        }
        public override async Task TransactionCommittedAsync(DbTransaction tx,TransactionEndEventData data,CancellationToken ct=default){await journal.Event("SEED_COMMIT_ACK",new{transactionId=data.TransactionId});owner.CommitAcknowledged=true;}
    }
}
