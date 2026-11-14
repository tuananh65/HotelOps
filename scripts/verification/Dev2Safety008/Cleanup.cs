using System.Data;
using Microsoft.Data.SqlClient;

namespace Safety008;

internal sealed record CleanupResult(string Status,bool CommitAcknowledged,bool IndependentPostcheck,int Deleted);
internal interface ICleanupStore
{
    Task Begin();Task<Snapshot> LockedSnapshot();Task Delete(Row row,Table table);Task Commit();Task Rollback();Task<Snapshot> IndependentSnapshot();
}
internal sealed class RealCleanupStore(SqlStore lease,Durable journal):ICleanupStore,IAsyncDisposable
{
    private SqlTransaction? tx;
    public async Task Begin(){await lease.RequireOwnLease();await lease.Quiet();tx=(SqlTransaction)await lease.Connection.BeginTransactionAsync(IsolationLevel.Serializable);await lease.Identity(tx);}
    public Task<Snapshot> LockedSnapshot()=>lease.Capture(tx??throw new InvalidOperationException("Transaction required"),true);
    public Task Delete(Row r,Table t)=>lease.DeleteExact(r,t,tx??throw new InvalidOperationException("Transaction required"));
    public async Task Commit(){await lease.Quiet(tx);await tx!.CommitAsync(CancellationToken.None);await tx.DisposeAsync();tx=null;}
    public async Task Rollback(){if(tx!=null){await tx.RollbackAsync(CancellationToken.None);await tx.DisposeAsync();tx=null;}}
    public async Task<Snapshot> IndependentSnapshot(){await using var c=new SqlConnection(lease.Connection.ConnectionString);await c.OpenAsync();var store=new SqlStore(c,journal);await store.Identity();await using var read=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);var result=await store.Capture(read);await read.RollbackAsync();return result;}
    public async ValueTask DisposeAsync(){if(tx!=null)await tx.DisposeAsync();}
}
internal sealed class CleanupAdapter(ICleanupStore store,Snapshot baseline,IReadOnlySet<string> seeded,IReadOnlyList<RootProof> actions,IReadOnlySet<long> actors,Durable journal,Func<bool>? controllerHealthy=null)
{
    private readonly object gate=new();private Task<CleanupResult>? finalization;
    internal Task<CleanupResult> FinalizeOnce(){lock(gate)return finalization??=Run();}
    internal static bool SameProtected(Snapshot expected,Snapshot actual)=>expected.SchemaHash==actual.SchemaHash&&expected.Rows.Count==actual.Rows.Count&&expected.Rows.All(p=>actual.Rows.TryGetValue(p.Key,out var r)&&r.Hash==p.Value.Hash);
    private async Task<CleanupResult> Run()
    {
        bool began=false,commitIntent=false,committed=false;var deleted=0;
        try{
            await store.Begin();began=true;var locked=await store.LockedSnapshot();var proof=Graph.Prove(baseline,locked,seeded,actions,actors);
            await journal.Event("CLEANUP_LOCKED_SQL_SNAPSHOT",locked);
            await journal.Event("CLEANUP_GRAPH_PROOF",new{owned=proof.Owned.Keys.ToArray(),problems=proof.Problems,protectedRows=baseline.Rows.Count,schemaHash=locked.SchemaHash});
            if(proof.Problems.Length>0)throw new InvalidOperationException("CLEANUP_OWNERSHIP_UNPROVEN_NO_DELETE");
            // Actual table locks are held. Re-read schema and ALL selected/unselected rows
            // before the first DELETE, guarding a trigger/schema change during acquisition.
            var predelete=await store.LockedSnapshot();if(!SameProtected(locked,predelete))throw new InvalidOperationException("PREDELETE_RECHECK_FAILED");
            foreach(var key in proof.DeleteOrder){var row=proof.Owned[key];await store.Delete(row,locked.Tables.Single(t=>t.Name==row.Table));deleted++;await journal.Event("EXACT_PK_DELETE_PENDING",new{row.Table,row.Pk,expectedRowCount=1});}
            var beforeCommit=await store.LockedSnapshot();
            if(!SameProtected(baseline,beforeCommit)||proof.Owned.Keys.Any(beforeCommit.Rows.ContainsKey))throw new InvalidOperationException("PROTECTED_ROW_OR_SELECTED_ROW_PRECOMMIT_MISMATCH");
            if(controllerHealthy?.Invoke()==false)throw new InvalidOperationException("CONTROLLER_LOST_ABORT_CLEANUP_COMMIT");
            await journal.Event("CLEANUP_COMMIT_INTENT",new{deleted,protectedRows=baseline.Rows.Count,allUnselectedRowsVerified=true});commitIntent=true;
            await store.Commit();committed=true;await journal.Event("CLEANUP_COMMIT_ACK",new{deleted});
            var after=await store.IndependentSnapshot();await journal.Event("CLEANUP_POSTCOMMIT_SQL_SNAPSHOT",after);if(!SameProtected(baseline,after))throw new InvalidOperationException("POSTCOMMIT_INDEPENDENT_PROOF_FAILED");
            await journal.Event("CLEANUP_INDEPENDENT_POSTCHECK_PASS",new{deleted,remainingOwned=0,protectedRows=baseline.Rows.Count});return new("PASS_EXACT_PK_CLEANUP",true,true,deleted);
        }catch(Exception e){
            if(committed){await journal.Event("CLEANUP_COMMITTED_POSTCHECK_UNCERTAIN",new{exceptionType=e.GetType().Name,deleted});return new("COMMITTED_POSTCHECK_UNCERTAIN",true,false,deleted);}
            if(commitIntent){await journal.Event("CLEANUP_COMMIT_UNCERTAIN_RECOVERY_REQUIRED",new{exceptionType=e.GetType().Name,deleted});return new("COMMIT_UNCERTAIN_NO_AUTOMATIC_RETRY",false,false,deleted);}
            if(began){try{await store.Rollback();await journal.Event("CLEANUP_ROLLBACK_ACK",new{exceptionType=e.GetType().Name,deleted});return new("ROLLED_BACK_FAIL_CLOSED",false,false,deleted);}catch{await journal.Event("CLEANUP_ROLLBACK_UNCONFIRMED",new{deleted});return new("ROLLBACK_UNCONFIRMED_RECOVERY_REQUIRED",false,false,deleted);}}
            await journal.Event("CLEANUP_NOT_STARTED",new{exceptionType=e.GetType().Name});return new("BLOCKED_NO_CLEANUP",false,false,0);
        }
    }
}
