using System.Diagnostics;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

internal static class OrphanRecovery
{
    internal static async Task Run(string output,string source,string marker,CancellationToken ct=default)
    {
        Directory.CreateDirectory(output);string phase="input-validation";bool committed=false;SqlTransaction? tx=null;HotelOpsDbContext? db=null;
        async Task Receipt(string name,object value)=>await FixtureRunSafety.Write(Path.Combine(output,name+".json"),value);
        try
        {
            if(marker!="D2A3994a0e4cd"||!FixtureRunSafety.ValidMarker(marker)||Path.GetFileName(source)!="HOST_20261113_005014_c06f6bac")throw new AcceptanceFailure("EXACT_APPROVED_ORPHAN_REQUIRED");
            using var manifest=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(source,"FIXTURE_MANIFEST.json"),ct));
            using(var preflight=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(source,"SQL_ENVIRONMENT_PREFLIGHT.json"),ct)))
            {
                var root=preflight.RootElement;var identity=root.GetProperty("identity");
                if(root.GetProperty("status").GetString()!="PASS"||identity.GetProperty("database_name").GetString()!="HotelOps_DEV2_FullStack_Trial"||identity.GetProperty("machine_name").GetString()!="KIENPC"||!string.Equals(identity.GetProperty("database_guid").GetString(),HostAcceptance.DatabaseGuid,StringComparison.OrdinalIgnoreCase))throw new AcceptanceFailure("HISTORICAL_PREFLIGHT_TARGET_MISMATCH");
            }
            foreach(var file in new[]{"EF_SCHEMA_COMPATIBILITY.json","BACKUP_AND_ROLLBACK.json"})
            {
                using var receipt=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(source,file),ct));var root=receipt.RootElement;
                if(!string.Equals(root.GetProperty("databaseGuid").GetString(),HostAcceptance.DatabaseGuid,StringComparison.OrdinalIgnoreCase)||(file.StartsWith("EF")?root.GetProperty("status").GetString()!="PASS":!root.GetProperty("backupVerified").GetBoolean()))throw new AcceptanceFailure("TARGET_BOUND_HISTORICAL_BACKUP_SCHEMA_REQUIRED");
            }
            using(var backup=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output,"RECOVERY_BACKUP.json"),ct)))
                if(!backup.RootElement.GetProperty("verified").GetBoolean()||!string.Equals(backup.RootElement.GetProperty("databaseGuid").GetString(),HostAcceptance.DatabaseGuid,StringComparison.OrdinalIgnoreCase))throw new AcceptanceFailure("FRESH_VERIFIED_BACKUP_REQUIRED");
            phase="process-quiescence";
            var guard=Environment.GetEnvironmentVariable("DEV2_RECOVERY_GUARD_SCRIPT")??throw new AcceptanceFailure("HOST_PROCESS_GUARD_REQUIRED");
            var start=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true};foreach(var arg in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",guard,"-AllowedProcessId",Environment.ProcessId.ToString(),"-OutputPath",Path.Combine(output,"RECOVERY_PROCESS_SCAN.json")})start.ArgumentList.Add(arg);
            using(var process=Process.Start(start)??throw new AcceptanceFailure("PROCESS_SCAN_NOT_STARTED")){await process.WaitForExitAsync(ct);if(process.ExitCode!=0)throw new AcceptanceFailure("ACTIVE_OR_UNINSPECTABLE_TRIAL_PROCESS");}
            var cs=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DEV2_TRIAL_CONNECTION")??throw new AcceptanceFailure("HOST_TRIAL_CONNECTION_REQUIRED"));
            if(cs.InitialCatalog!="HotelOps_DEV2_FullStack_Trial"||!cs.IntegratedSecurity||cs.DataSource is not("KIENPC" or "lpc:KIENPC"))throw new AcceptanceFailure("EXACT_TRIAL_WINDOWS_AUTH_REQUIRED");
            cs.ApplicationName="DEV2_004_RECOVERY";cs.Pooling=false;
            db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(cs.ConnectionString).Options);
            phase="identity-and-exclusive-lease";await FixtureRunSafety.Lease(db,ct);
            var connection=(SqlConnection)db.Database.GetDbConnection();await Receipt("DATABASE_IDENTITY",new{status="PASS",database=cs.InitialCatalog,databaseGuid=HostAcceptance.DatabaseGuid,machine="KIENPC",defaultInstance=true});
            tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            phase="read-locked-snapshot-and-prove-ownership";var before=await RecoverySnapshot.Read(connection,tx,ct);
            await Receipt("RECOVERY_SNAPSHOT_COUNTS",new{status="READ_ONLY_PRE_DELETE",counts=before.Tables.Values.ToDictionary(t=>t.Id,t=>t.Rows.Count)});
            var proof=new RecoveryOwnership(before,db,manifest.RootElement,marker);var owned=proof.Build();var order=RecoverySnapshot.DeleteOrder(owned,before.Edges());
            var keys=owned.Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);var outsideHash=before.PreservedHash(keys);
            await Receipt("RECOVERY_PLAN",new{status="OWNERSHIP_PROVEN_NOT_YET_DELETED",marker,ownedCounts=owned.GroupBy(x=>x.Table.Id).ToDictionary(g=>g.Key,g=>g.Count()),ownedIds=owned.Select(r=>new{table=r.Table.Id,primaryKey=r.Table.PrimaryKey.ToDictionary(c=>c,c=>r.Values[c])}),unrelatedRowHashBefore=outsideHash,allUserTablesLocked=true,deletePolicy="Exact catalog-derived primary keys only; external references/cycles abort"});
            phase="exact-primary-key-deletion";foreach(var row in order)await RecoverySnapshot.Delete(row,connection,tx,ct);
            phase="verify-before-commit";var after=await RecoverySnapshot.Read(connection,tx,ct);
            if(after.Tables.Values.SelectMany(t=>t.Rows).Any(r=>r.Table.PrimaryKey.Count>0&&keys.Contains(r.Key))||after.PreservedHash(keys)!=outsideHash)throw new AcceptanceFailure("RECOVERY_ROOTS_OR_UNRELATED_DATA_CHANGED");
            await Receipt("RECOVERY_PRECOMMIT_VERIFICATION",new{status="PASS",marker,ownedRowsBefore=owned.Count,ownedRowsAfter=0,unrelatedRowHashAfter=after.PreservedHash(keys),sharedDataPreserved=true,borrowedPriorityPreserved=true,transaction="PENDING_COMMIT"});
            phase="commit";await tx.CommitAsync(ct);committed=true;
            phase="postcommit-orphan-verification";var remaining=await FixtureRunSafety.Orphans(db,ct);
            if(remaining.Contains(marker))throw new AcceptanceFailure("ORPHAN_ROOTS_REMAIN_AFTER_COMMIT");
            await Receipt("RECOVERY_RESULT",new{status="PASS",RECOVERY_MARKER=marker,DATABASE_GUID_VERIFIED=true,ORPHAN_ROOTS_BEFORE=owned.Count,ORPHAN_ROOTS_AFTER=0,SHARED_DATA_PRESERVED=true,CLEANUP_TRANSACTION="COMMITTED",SAFE_TO_RERUN_FULL61=remaining.Length==0,remainingOrphanMarkers=remaining,INTERRUPTION_ROOT_CAUSE="UNDETERMINED_NO_TERMINATION_RECEIPT",interruptionMitigation="Durable native-process and fixture intent/manifest journals; orphan gate; exclusive harness lease",sqlEffects="Exact owned rows deleted; all unrelated user-table scalar row hashes unchanged within exclusive transaction"});
        }
        catch(Exception error)
        {
            if(tx is not null&&!committed)try{await tx.RollbackAsync(CancellationToken.None);await Receipt("RECOVERY_ROLLBACK",new{status="ROLLBACK_ACKNOWLEDGED",phase,commitAcknowledgementUnknown=phase=="commit"});}catch(Exception rollback){await Receipt("RECOVERY_ROLLBACK_FAILURE",new{exceptionType=rollback.GetType().Name,automaticRestore=false});}
            await Receipt("RECOVERY_FAILURE",new{status="FAIL_CLOSED",marker,phase,commitAcknowledged=committed,commitOutcome=committed?"COMMITTED":phase=="commit"?"UNKNOWN_ACKNOWLEDGEMENT":"NOT_COMMITTED",diagnostic=FailureDiagnostics.Capture(error,phase,null,null,null,output),ownershipTable=error.Data["DEV2.Recovery.Table"] as string,ownershipProperty=error.Data["DEV2.Recovery.Property"] as string,ownershipKeyHash=error.Data["DEV2.Recovery.KeyHash"] as string,SAFE_TO_RERUN_FULL61=false});Environment.ExitCode=1;
        }
        finally{if(tx is not null)await tx.DisposeAsync();if(db is not null)await db.DisposeAsync();}
    }
}
