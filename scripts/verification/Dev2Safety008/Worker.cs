using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using HotelOps.Api.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Safety008;

internal sealed record Bootstrap(int ControllerPid,int ControllerParentPid,string ControllerExecutable,DateTime ControllerStartedUtc,string Frontend,string NodeExecutable,string BrowserScript,ProcessIdentity[] ApiFrontendProcesses);
internal sealed class Worker
{
    internal static async Task Run(string mode,string authorityPath,string release,string repo,string output)
    {
        if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("COMPUTERNAME")!="KIENPC")throw new InvalidOperationException("Windows host only; no sandbox SQL retry");
        HostAcceptance.RequireNormalEnvironment();
        var authority=await Authorization.Verify(authorityPath,release,repo,mode,output);
        Console.WriteLine("CONTROL:AUTHORIZATION_VALIDATED");
        var bootstrap=JsonSerializer.Deserialize<Bootstrap>(await Console.In.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(60))??"",Durable.Json)??throw new InvalidOperationException("Controller bootstrap required");
        if(!string.Equals(Path.GetFullPath(bootstrap.BrowserScript),Path.GetFullPath(Path.Combine(repo,"scripts/verification/Dev2Safety008/browser/child.mjs")),StringComparison.OrdinalIgnoreCase)||bootstrap.Frontend!="https://localhost:5173")throw new InvalidOperationException("Only the sealed candidate browser and fixed local frontend may execute");
        var controller=Process.GetProcessById(bootstrap.ControllerPid);
        var identity=new ProcessIdentity(bootstrap.ControllerPid,bootstrap.ControllerParentPid,bootstrap.ControllerExecutable,bootstrap.ControllerStartedUtc);
        if(!identity.Matches(controller))throw new InvalidOperationException("Exact controller identity required");
        if(bootstrap.ApiFrontendProcesses.Length!=0)throw new InvalidOperationException("No API/frontend before fixture preflight and baseline capture");
        var ownedRuntime=new List<ProcessIdentity>();
        var journal=new Durable(Path.Combine(output,"journal"));
        var messages=Channel.CreateUnbounded<string>();var heartbeatTicks=DateTime.UtcNow.Ticks;var eof=false;
        _=Task.Run(async()=>{try{while(await Console.In.ReadLineAsync() is { } line){if(line=="HEARTBEAT")Interlocked.Exchange(ref heartbeatTicks,DateTime.UtcNow.Ticks);else await messages.Writer.WriteAsync(line);}}finally{Volatile.Write(ref eof,true);}});
        bool Healthy(){try{return !Volatile.Read(ref eof)&&DateTime.UtcNow.Ticks-Interlocked.Read(ref heartbeatTicks)<TimeSpan.FromSeconds(10).Ticks&&identity.Matches(controller);}catch{return false;}}
        await journal.Event("CONTROLLER_AND_HARNESS_BOUND",new{authority.Run,bootstrap.ControllerPid,bootstrap.ControllerStartedUtc,harnessPid=Environment.ProcessId,logicalHarnessOwner="THIS_PROCESS",output,mode});
        var connectionString="Data Source=lpc:KIENPC;Initial Catalog=HotelOps_DEV2_FullStack_Trial;Integrated Security=True;Encrypt=False;Pooling=False;Connect Timeout=10;Application Name=DEV2_SAFETY008";
        await using var leaseConnection=new SqlConnection(connectionString);await leaseConnection.OpenAsync();var store=new SqlStore(leaseConnection,journal);
        bool leased=false,mutationAttempted=false;var finalStatus="FAIL_RECOVERY_REQUIRED";Fixtures? fixtures=null;SeedLedger? ledger=null;
        try{
            await store.Identity();await store.Quiet();await store.AcquireLease();leased=true;
            await using var read=(SqlTransaction)await leaseConnection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var baseline=await store.Capture(read);await read.RollbackAsync();
            if(baseline.EnabledTriggers.Length>0)throw new InvalidOperationException("Unreviewed triggers; no fixture mutation");
            await Durable.File(Path.Combine(output,"PROTECTED_PRE_FIXTURE_SNAPSHOT.json"),baseline);
            ledger=new SeedLedger(baseline,journal,store,Healthy);
            var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connectionString).AddInterceptors(ledger,ledger.TransactionGuard()).Options;
            HotelOpsDbContext Open()=>new(options);
            await using(var model=Open())await store.ValidateEf(model,baseline);
            // Check globally for any fixture-prefix roots before creating a new marker.
            var orphan=await store.Query("SELECT (SELECT COUNT_BIG(*) FROM dbo.users WHERE username LIKE N'D2A%')+(SELECT COUNT_BIG(*) FROM dbo.branches WHERE branch_code LIKE N'D2A%')+(SELECT COUNT_BIG(*) FROM dbo.roles WHERE role_code LIKE N'D2A%')+(SELECT COUNT_BIG(*) FROM dbo.item_categories WHERE category_code LIKE N'D2A%')+(SELECT COUNT_BIG(*) FROM dbo.reference_values WHERE value_code LIKE N'D2A%') roots");
            if(orphan.Single()["roots"]!="0"||!Healthy())throw new InvalidOperationException("Orphan or controller gate blocked");
            fixtures=new Fixtures(Open,output);var session=new Session(authority.Run,fixtures.Marker,SqlStore.DatabaseGuid,Authorization.HashFile(release));
            await Durable.File(Path.Combine(output,"SESSION.json"),session);await journal.Event("FIXTURE_MUTATION_INTENT",session);
            mutationAttempted=true;await fixtures.Create();await journal.Event("FIXTURE_COMMITTED_EXACT_KEYS_DURABLE",new{session,owned=ledger.Keys.Count});
            var actors=fixtures.UserIds.ToHashSet();var roots=new List<RootProof>();ChildOutcome outcome=new(ChildKind.NormalBrowserExit,0,true);
            if(mode!="fixture-acceptance"){
                if(!Healthy())throw new InvalidOperationException("Controller lost before browser startup");
                Console.WriteLine("CONTROL:START_API_FRONTEND_AFTER_FIXTURE");
                var runtimeLine=await WaitRuntime(messages.Reader,Healthy,TimeSpan.FromSeconds(120));
                var runtime=JsonSerializer.Deserialize<ProcessIdentity[]>(runtimeLine,Durable.Json)??throw new InvalidOperationException("Runtime identities required");
                if(runtime.Length!=2)throw new InvalidOperationException("Exactly owned API/frontend roots required");
                foreach(var registered in runtime){using var process=Process.GetProcessById(registered.Pid);if(registered.ParentPid!=bootstrap.ControllerPid||!registered.Matches(process))throw new InvalidOperationException("API/frontend process ownership mismatch");ownedRuntime.AddRange(ProcessTree.CaptureTree(registered.Pid,bootstrap.ControllerPid));}
                await journal.Event("OWNED_API_FRONTEND_STARTED",ownedRuntime);
                if(!Uri.TryCreate(bootstrap.Frontend,UriKind.Absolute,out var frontend)||!frontend.IsLoopback||frontend.Scheme!="https")throw new InvalidOperationException("Local trusted HTTPS frontend required");
                using var ownerStop=new CancellationTokenSource();using var pipeStop=new CancellationTokenSource();
                var validator=new StopValidator(session,RandomNumberGenerator.GetBytes(32));var pipe=OwnerStopPipe.Listen(session,validator,journal,()=>ownerStop.Cancel(),pipeStop.Token);
                var stopFiles=OwnerStopPipe.Poll(session,validator,journal,()=>ownerStop.Cancel(),pipeStop.Token);
                outcome=await new Lifecycle(journal).Supervise(ct=>RunBrowser(bootstrap,fixtures,session,roots,store,baseline,ledger,actors,journal,mode,ct),Healthy,TimeSpan.FromMinutes(authority.MaximumMinutes),ownerStop.Token);
                pipeStop.Cancel();await pipe;try{await stopFiles;}catch(OperationCanceledException)when(pipeStop.IsCancellationRequested){}
            }
            await journal.Event("CLEANUP_REQUIRES_OWNED_API_FRONTEND_STOP",new{outcome.Kind,outcome.Quiesced});
            foreach(var root in ownedRuntime.Where(p=>p.ParentPid==bootstrap.ControllerPid).ToArray())ownedRuntime.AddRange(ProcessTree.CaptureTree(root.Pid,bootstrap.ControllerPid));
            Console.WriteLine("CONTROL:QUIESCE_API_FRONTEND");
            var stopAck=await WaitControl(messages.Reader,"API_FRONTEND_STOPPED",Healthy,TimeSpan.FromSeconds(60));
            if(!stopAck||!outcome.Quiesced||!Healthy())throw new InvalidOperationException("No verified quiescence; no cleanup attempted");
            ProcessTree.RequireAbsent(ownedRuntime);await journal.Event("API_FRONTEND_ABSENCE_VERIFIED",new{ownedIdentities=ownedRuntime});
            await store.Quiet();await store.RequireOwnLease();
            var current=await store.Capture();var proof=Graph.Prove(baseline,current,ledger.Keys,roots,actors);
            await Durable.File(Path.Combine(output,"FINAL_OWNERSHIP_BEFORE_CLEANUP.json"),new{proof,actionRoots=roots});
            await using var sqlCleanup=new RealCleanupStore(store,journal);
            var result=await new CleanupAdapter(sqlCleanup,baseline,ledger.Keys,roots,actors,journal,Healthy).FinalizeOnce();
            await Durable.File(Path.Combine(output,"EXACT_PK_CLEANUP_RESULT.json"),result);
            if(result.Status!="PASS_EXACT_PK_CLEANUP")throw new InvalidOperationException("Cleanup not certified; no automatic retry");
            finalStatus=outcome.NativeExit==0&&outcome.Kind==ChildKind.NormalBrowserExit?"PASS_CANDIDATE_SAFETY_REQUIRES_REVIEW":"FAIL_CHILD_OUTCOME_CLEANUP_VERIFIED";
            // Browser safety receipt is distinct from scenario/SQL effects acceptance.
            await Durable.File(Path.Combine(output,"BROWSER_OUTCOME.json"),outcome);
        }catch(Exception error){await journal.Event("HARNESS_FAILURE_NO_FINALLY_CLEANUP",new{exceptionType=error.GetType().Name,fixtureCommitted=fixtures?.Committed,seedCommitIntent=ledger?.CommitIntent,seedCommitAcknowledged=ledger?.CommitAcknowledged,seedCommitOutcome=ledger?.CommitIntent==true&&ledger.CommitAcknowledged==false?"UNCERTAIN_NO_AUTOMATIC_RETRY":"SEE_DURABLE_JOURNAL_NO_ROLLBACK_INFERRED",automaticCleanupAttempt=false});}
        finally{
            if(fixtures!=null)fixtures.Password="";
            if(leased){try{await store.ReleaseLease();}catch{finalStatus="FAIL_LEASE_RELEASE_UNCONFIRMED";await journal.Event("LEASE_RELEASE_UNCONFIRMED",new{recoveryRequired=true});}}
            await journal.Event("HARNESS_TERMINAL",new{status=finalStatus,cleanupGuaranteedAfterForceKill=false});
            await Durable.File(Path.Combine(output,"CANDIDATE_SAFETY_RESULT.json"),new{status=finalStatus,checkpoint008DemoAccepted=false,sqlMutationAttempted=mutationAttempted,sourceScope="NEW_CANDIDATE_NOT_ACCEPTED007",mode});
            Durable.Verify(journal.DirectoryPath);
            var files=Directory.EnumerateFiles(output,"*",SearchOption.AllDirectories).Where(f=>!f.EndsWith(".pending",StringComparison.Ordinal)).Order(StringComparer.Ordinal).Select(f=>new{file=Path.GetRelativePath(output,f).Replace('\\','/'),sha256=Authorization.HashFile(f)}).ToArray();
            await Durable.File(Path.Combine(output,"CANDIDATE_RECEIPT_SHA256.json"),files);
            if(!finalStatus.StartsWith("PASS_",StringComparison.Ordinal))Environment.ExitCode=1;
        }
    }
    private static async Task<bool> WaitControl(ChannelReader<string> messages,string expected,Func<bool> healthy,TimeSpan timeout)
    {
        var end=DateTime.UtcNow+timeout;while(DateTime.UtcNow<end&&healthy()){if(messages.TryRead(out var line)&&line==expected)return true;await Task.Delay(50);}return false;
    }
    private static async Task<string> WaitRuntime(ChannelReader<string> messages,Func<bool> healthy,TimeSpan timeout)
    {
        var end=DateTime.UtcNow+timeout;while(DateTime.UtcNow<end&&healthy()){if(messages.TryRead(out var line)&&line.StartsWith("RUNTIME_READY:",StringComparison.Ordinal))return line[14..];await Task.Delay(50);}throw new InvalidOperationException("Owned runtime startup not confirmed");
    }
    private static async Task<ChildOutcome> RunBrowser(Bootstrap b,Fixtures fixtures,Session session,List<RootProof> roots,SqlStore store,Snapshot baseline,SeedLedger ledger,HashSet<long> actors,Durable journal,string mode,CancellationToken ct)
    {
        var start=new ProcessStartInfo(b.NodeExecutable){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};start.ArgumentList.Add(b.BrowserScript);start.Environment.Remove("DEBUG");start.Environment.Remove("PWDEBUG");
        using var child=Process.Start(start)??throw new InvalidOperationException("Child not started");var id=ProcessIdentity.Capture(child,Environment.ProcessId);await journal.Event("BROWSER_CHILD_STARTED_EXACT_IDENTITY",id);
        await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{session,frontend=b.Frontend,output=journal.DirectoryPath,password=fixtures.Password,users=fixtures.Users.Select(x=>new{x.Username,x.UserId}),branchId=fixtures.Branch.BranchId,departmentId=fixtures.Department.DepartmentId,areaId=fixtures.Area.AreaId,warehouseId=fixtures.Warehouse.WarehouseId,itemId=fixtures.Items[1].ItemId,uomId=fixtures.Uom.UomId,assetId=fixtures.Assets[16].AssetId,timeoutMinutes=120,mode},Durable.Json));
        var terminal=false;var closeTreeAcknowledged=false;var kind=ChildKind.Failure;var browserIdentities=new List<ProcessIdentity>();int? browserPid=null;var cookieActors=new HashSet<long>();
        var errors=Task.Run(async()=>{while(await child.StandardError.ReadLineAsync()!=null){/* Never export arbitrary stderr which may include private page payloads. */}});
        using var cancel=ct.Register(()=>{try{child.StandardInput.WriteLine(JsonSerializer.Serialize(new{command="QUIESCE",run=session.Run,marker=session.Marker,databaseGuid=session.DatabaseGuid,sourceIdentity=session.SourceIdentity},Durable.Json));child.StandardInput.Flush();}catch{}});
        while(await child.StandardOutput.ReadLineAsync() is {} line){
            if(line.Length>65536)throw new InvalidOperationException("Oversize child record");using var message=JsonDocument.Parse(line);var r=message.RootElement;
            if(r.GetProperty("type").GetString()=="browser-process"){browserPid=r.GetProperty("pid").GetInt32();browserIdentities.AddRange(ProcessTree.CaptureTree(browserPid.Value,child.Id));await journal.Event("BROWSER_TREE_IDENTITIES",browserIdentities);continue;}
            if(r.GetProperty("type").GetString()=="pre-browser-close"){
                if(browserPid!=null){browserIdentities.AddRange(ProcessTree.CaptureTree(browserPid.Value,child.Id));await journal.Event("BROWSER_CLOSE_TREE_CAPTURED",browserIdentities);}
                await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new{command="CLOSE_TREE_CAPTURED",run=session.Run,marker=session.Marker,databaseGuid=session.DatabaseGuid,sourceIdentity=session.SourceIdentity},Durable.Json));continue;
            }
            if(r.GetProperty("type").GetString()=="terminal"){terminal=true;closeTreeAcknowledged=r.GetProperty("closeTreeAcknowledged").GetBoolean();kind=Enum.Parse<ChildKind>(r.GetProperty("kind").GetString()!);continue;}
            if(r.GetProperty("type").GetString()=="cookie-authenticated"){var cookieActor=r.GetProperty("actor").GetInt64();if(!actors.Contains(cookieActor)||!r.GetProperty("secureHttpOnlyCookie").GetBoolean())throw new InvalidOperationException("Cookie actor not bound");cookieActors.Add(cookieActor);await journal.Event("REAL_COOKIE_CONTEXT_AUTHENTICATED",new{actor=cookieActor,secure=true,httpOnly=true,separateContext=true});continue;}
            if(r.GetProperty("type").GetString()!="action")continue;
            var actor=r.GetProperty("actor").GetInt64();if(!actors.Contains(actor))throw new InvalidOperationException("Unbound actor record");
            var method=r.GetProperty("method").GetString()!;var route=r.GetProperty("route").GetString()!;var status=r.GetProperty("status").GetInt32();
            await journal.Event("REAL_BROWSER_HTTP",new{actor,method,route,status});
            foreach(var root in r.GetProperty("roots").EnumerateArray()){
                var pk=JsonSerializer.Deserialize<SortedDictionary<string,string?>>(root.GetProperty("pk").GetRawText(),Durable.Json)!;
                var proof=new RootProof(root.GetProperty("table").GetString()!,pk,actor,method,route,status);
                if(!Graph.RootRoute(proof)||baseline.Rows.ContainsKey(proof.Key))throw new InvalidOperationException("Browser root not a new allowed aggregate");
                if(!roots.Any(p=>p.Key==proof.Key)){roots.Add(proof);await journal.Event("INTERACTIVE_RETURNED_ROOT_PK",proof);}
            }
            // Independent SQL observes all new dependents after the committed response.
            var snapshot=await store.Capture();var graph=Graph.Prove(baseline,snapshot,ledger.Keys,roots,actors);
            await journal.Event("INTERACTIVE_SQL_SNAPSHOT_CAPTURED",snapshot);
            foreach(var row in graph.Owned.Values.Where(row=>!ledger.Keys.Contains(row.Key)))await journal.Event("INTERACTIVE_SQL_DEPENDENT_PK_OBSERVED",new{row.Table,row.Pk,row.Hash,provenance="REAL_SQL_GRAPH_AND_RECORDED_ROOT_AUDIT_LINEAGE"});
            await journal.Event("INTERACTIVE_GRAPH_OBSERVATION",new{problems=graph.Problems,allRows=snapshot.Rows.Count});
        }
        await child.WaitForExitAsync();await errors;
        ProcessTree.RequireAbsent(browserIdentities);await journal.Event("BROWSER_TREE_ABSENCE_VERIFIED",new{trackedIdentities=browserIdentities.Count});
        await journal.Event("BROWSER_NATIVE_EXIT",new{id.Pid,child.ExitCode,terminal});
        if(mode=="browser-acceptance"&&(cookieActors.Count!=3||roots.Count!=1))kind=ChildKind.Failure;
        return new(kind,child.ExitCode,terminal&&child.HasExited&&browserIdentities.Count>0&&closeTreeAcknowledged);
    }
}
