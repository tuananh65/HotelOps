using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class ImplementationChecks
{
    internal static async Task<IReadOnlyList<string>> RunAsync()
    {
        var checks=new List<string>();
        void Check(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
        async Task Deny<T>(Func<Task> run,string name) where T:Exception
        {try{await run();}catch(T){checks.Add(name);return;}throw new Exception("Expected "+typeof(T).Name+": "+name);}
        var request=new Fe05OperationRequest(Guid.Parse("20261109-0000-4000-8000-000000000001"),7,11,new string('A',64),
            [Fe05NotificationKind.InspectionFailed,Fe05NotificationKind.CleaningReworkRequested]);
        await Deny<InvalidOperationException>(()=>Task.Run(()=>N01SqlComponentProbe.RequireLease("absent/SQL_TRIAL_EXCLUSIVE.lock")),"SQL probe missing exclusive lease rejects before SQL/configuration");
        Check(Fe05OperationIdentity.Fingerprint(request)==Fe05OperationIdentity.Fingerprint(request with{RequiredEvents=request.RequiredEvents.Reverse().ToArray()}),"Operation event set canonicalized before reservation");
        Check(Fe05OperationIdentity.Fingerprint(request)!=Fe05OperationIdentity.Fingerprint(request with{RequestFingerprint=new string('B',64)}),"Changed canonical mutation request changes ledger fingerprint");
        Check(Fe05OperationIdentity.Fingerprint(request)!=Fe05OperationIdentity.Fingerprint(request with{RequiredEvents=[Fe05NotificationKind.InspectionFailed]}),"Missing mandatory event changes operation identity");
        await Deny<Fe05NotificationUnavailableException>(()=>Task.FromResult(Fe05OperationIdentity.Fingerprint(request with{RequestFingerprint="bad"})),"Malformed command digest rejected");
        await Deny<Fe05NotificationUnavailableException>(()=>Task.FromResult(Fe05OperationIdentity.Fingerprint(request with{RequiredEvents=[Fe05NotificationKind.InspectionFailed,Fe05NotificationKind.InspectionFailed]})),"Duplicate mandatory event rejected");
        var store=new MemoryStore();
        await using(var h=await Harness.OpenAsync(store))
        {
            var lease=await h.Reserve(request);
            Check(lease.Replay is null && store.Operations.Count==1 && store.Events.Count==0,"Reservation creates no publication before domain mutation");
            await Deny<InvalidOperationException>(()=>h.Guard.VerifyBeforeCommitAsync(h.Db,default),"Existing commit guard rejects reserved but unstaged operation");
            var receipt=await h.Stage(lease,request);
            Check(receipt.Publications.Count==2 && store.Events.Count==2 && store.Events.Values.All(e=>e.RecipientIds.SequenceEqual(new long[]{31,37})),"Actual orchestration stages both events and all mandatory recipients");
            Check(receipt.Durability=="CALLER_TRANSACTION_NOT_COMMITTED","Staging receipt never claims caller commit");
            Check(store.Operations.Values.Single().DomainResultJson=="{\"testOwnerReceipt\":true}","N01 staging does not replace owner domain result_json");
            await h.Guard.VerifyBeforeCommitAsync(h.Db,default);
            await h.Tx.CommitAsync();
            Check(h.Connection.Commits==1 && store.Verifications>=2,"EF asynchronous commit invokes existing graph verifier");
            Check(!h.Db.ChangeTracker.Entries().Any(),"Single graph path creates no C04 tracked intent");
        }
        await using(var h=await Harness.OpenAsync(store))
        {
            var before=store.Events.Count;
            var lease=await h.Reserve(request);
            Check(lease.Replay is not null && store.Events.Count==before && h.Source.Loads==0,"Identical recovery replay finds receipt before source or domain mutation");
            await h.Tx.CommitAsync();
            Check(store.Events.Count==before,"Replay commit does not fan out again");
        }
        await using(var h=await Harness.OpenAsync(store))
        {
            await Deny<NotificationConflictException>(()=>h.Reserve(request with{RequestFingerprint=new string('B',64)}),"Same operation changed fingerprint rejects before domain mutation");
            await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Swallowed reservation conflict poisons caller EF commit");
            Check(h.Connection.Commits==0,"Poisoned guard never reaches provider commit");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            await Deny<NotificationConflictException>(()=>h.Service.StageRequiredAsync(h.Connection,h.Tx.GetDbTransaction(),lease,[h.Command(request,Fe05NotificationKind.InspectionFailed)],default),"Incomplete required event bundle rejects");
            await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Missing required event prevents EF commit even if caller catches error");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore{FailSecondWrite=true}))
        {
            var lease=await h.Reserve(request);
            await Deny<InvalidOperationException>(()=>h.Stage(lease,request),"Failure during second event propagates");
            await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Partial fan-out failure poisons caller commit");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            h.Recipients.Deny=true;
            await Deny<Fe05NotificationUnavailableException>(()=>h.Stage(lease,request),"Recipient authorization failure rejects before publication writes");
            Check(h.Store.Events.Count==0,"All mandatory eligibility checked before first graph insert");
            await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Recipient failure poisons caller commit");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            h.Source.Stale=true;
            await Deny<NotificationConflictException>(()=>h.Stage(lease,request),"Post-mutation stale revision is checked by actual staging preparation");
            Check(h.Store.Events.Count==0,"Stale source produces no publication");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        await using(var other=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            await Deny<InvalidOperationException>(()=>other.Stage(lease,request),"Lease cannot cross scoped service or transaction");
            await Deny<InvalidOperationException>(()=>h.Service.ReserveAsync(other.Connection,other.Tx.GetDbTransaction(),request,default),"Physical connection/transaction mismatch rejected");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            await h.Stage(lease,request);
            h.Store.Events.Clear();
            await Deny<NotificationConflictException>(()=>h.Tx.CommitAsync(),"Graph deletion after staging is detected at actual EF commit interception");
            Check(h.Connection.Commits==0,"Changed graph cannot reach provider commit");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            await h.Stage(lease,request);
            h.Source.Stale=true;
            await Deny<NotificationConflictException>(()=>h.Tx.CommitAsync(),"Source change after staging rejected by final commit reread");
            Check(h.Connection.Commits==0,"Source divergence never reaches provider commit");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            await h.Stage(lease,request);
            h.Recipients.Deny=true;
            await Deny<Fe05NotificationUnavailableException>(()=>h.Tx.CommitAsync(),"Recipient read revocation after staging rejected by commit reread");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            await Deny<OperationCanceledException>(()=>h.Service.StageRequiredAsync(h.Connection,h.Tx.GetDbTransaction(),lease,request.RequiredEvents.Select(k=>h.Command(request,k)).ToArray(),new(true)),"Staging cancellation propagates");
            await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Cancelled required staging poisons later caller commit");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var lease=await h.Reserve(request);
            _=await h.Service.StageRequiredAsync(h.Connection,h.Tx.GetDbTransaction(),lease,request.RequiredEvents.Select(k=>h.Command(request,k)).ToArray(),default);
            Check(!h.Store.Operations.Values.Single().Completed && h.Store.Operations.Values.Single().DomainResultJson is null,"Actual staging leaves completion/domain receipt to owner");
            await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"N01 receipt alone cannot commit without owner completion");
        }
        await using(var h=await Harness.OpenAsync(new MemoryStore()))
        {
            var sqlSource=new Fe05SqlNotificationSource(h.Db,new Actor(),new Granted(),new UnacceptedFe05Coordination());
            await Deny<Fe05NotificationUnavailableException>(()=>sqlSource.LoadLockedAsync(h.Command(request,Fe05NotificationKind.InspectionFailed),3,default),"Actual SQL source refuses unaccepted owner authority before SQL reads");
            var resolver=new Fe05SqlRecipientPolicy(h.Db,new UnacceptedFe05MandatoryRecipients(),new Coordination(),TimeProvider.System);
            await Deny<Fe05NotificationUnavailableException>(()=>resolver.ResolveAsync(new Source().Snapshot(Fe05NotificationKind.InspectionFailed),Fe05NotificationKind.InspectionFailed,default),"Actual eligibility reader refuses unapproved mandatory recipient policy");
            var sqlStore=new Fe05Fs013CandidateStore(h.Db,new DeniedExecution());
            await Deny<Fe05NotificationUnavailableException>(()=>sqlStore.RequireAvailableAsync(default),"Actual SQL store requires runtime exclusive-lease authorization before SQL");
            await Deny<OperationCanceledException>(()=>sqlSource.LoadLockedAsync(h.Command(request,Fe05NotificationKind.InspectionFailed),3,new(true)),"SQL source preserves cancellation before authorization or query");
            Check(h.Connection.Commands==0,"Negative SQL adapter tests execute zero provider commands");
        }
        return checks;
    }
    private sealed class Harness : IAsyncDisposable
    {
        public StubConnection Connection {get;}=new(); public NotificationCommitGuard Guard{get;}=new();
        public HotelOpsDbContext Db{get;} public IDbContextTransaction Tx{get;private set;}=null!;
        public Source Source{get;}=new();public Recipients Recipients{get;}=new(); public MemoryStore Store{get;}
        public Fe05OperationStaging Service{get;}
        private Harness(MemoryStore store)
        {
            Store=store;Db=new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).AddInterceptors(Guard).Options);
            var preparation=new Fe05NotificationStaging(Db,Guard,new Actor(),Source,Recipients);
            Service=new(Db,Guard,new Actor(),new Coordination(),preparation,store);
        }
        public static async Task<Harness> OpenAsync(MemoryStore store){var h=new Harness(store);h.Tx=await h.Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);return h;}
        public Task<Fe05OperationLease> Reserve(Fe05OperationRequest r)=>Service.ReserveAsync(Connection,Tx.GetDbTransaction(),r,default);
        public Fe05NotificationCommand Command(Fe05OperationRequest r,Fe05NotificationKind k)=>new(k,r.OperationId,r.BranchId,r.InspectionId,"\"0000000000000001\"");
        public async Task<Fe05OperationReceipt> Stage(Fe05OperationLease l,Fe05OperationRequest r)
        {
            var receipt=await Service.StageRequiredAsync(Connection,Tx.GetDbTransaction(),l,r.RequiredEvents.Select(k=>Command(r,k)).ToArray(),default);
            // Explicit owner simulation only in this unit fixture; production staging never does this.
            Store.OwnerFinalize(3,r.OperationId);return receipt;
        }
        public async ValueTask DisposeAsync(){await Tx.DisposeAsync();await Db.DisposeAsync();}
    }
    private sealed class Actor:ICurrentUserAccessor {public bool TryGetCurrentUser(out CurrentUserInfo user){user=new(3,5,false);return true;}}
    private sealed class Granted:IPermissionService
    {
        public Task<PermissionEvaluation> EvaluateBranchAsync(string p,long b,CancellationToken ct)=>Task.FromResult(new PermissionEvaluation(PermissionDecision.Granted));
        public Task<PermissionEvaluation> EvaluateGlobalAsync(string p,CancellationToken ct)=>EvaluateBranchAsync(p,1,ct);
        public Task<PermissionEvaluation> EvaluateResourceAsync(string p,PermissionResource r,CancellationToken ct)=>EvaluateBranchAsync(p,1,ct);
    }
    private sealed class Coordination:IFe05SourceCoordination
    {
        public Task RequireReservationAsync(HotelOpsDbContext d,Fe05OperationRequest r,CurrentUserInfo a,CancellationToken ct)=>Task.CompletedTask;
        public Task RequireCompletedOperationAsync(HotelOpsDbContext d,Fe05OperationRequest r,CurrentUserInfo a,Fe05OperationReceipt receipt,CancellationToken ct)=>Task.CompletedTask;
        public Task RequireAuthorityAsync(HotelOpsDbContext d,Fe05NotificationCommand c,CurrentUserInfo a,CancellationToken ct)=>Task.CompletedTask;
        public Task<bool> CanRecipientReadAsync(HotelOpsDbContext d,Fe05NotificationSource s,long id,CancellationToken ct)=>Task.FromResult(true);
    }
    private sealed class Source:IFe05NotificationSource
    {
        public int Loads;public bool Stale;
        public Fe05NotificationSource Snapshot(Fe05NotificationKind kind)=>new(3,7,13,11,17,19,
            kind==Fe05NotificationKind.CleaningReworkRequested?41:null,Stale?"stale":"\"0000000000000001\"",17,"COMPLETED","FAIL","FAILED",DateTimeOffset.Parse("2026-11-09T00:00:00Z"),[23,29],kind==Fe05NotificationKind.CleaningReworkRequested?"\"0000000000000001\"":null);
        public Task<Fe05NotificationSource> LoadLockedAsync(Fe05NotificationCommand c,long actor,CancellationToken ct){Loads++;return Task.FromResult(Snapshot(c.Kind));}
    }
    private sealed class Recipients:IFe05NotificationRecipientPolicy
    {
        public bool Deny;
        public Task<IReadOnlyList<Fe05Recipient>> ResolveAsync(Fe05NotificationSource s,Fe05NotificationKind k,CancellationToken ct)=>Deny
            ?throw new Fe05NotificationUnavailableException("test recipient denied"):Task.FromResult<IReadOnlyList<Fe05Recipient>>([new(31,7,true,true),new(37,7,true,true)]);
    }
    private sealed class DeniedExecution:IFe05TrialExecutionAuthorization
    {public Task RequireAsync(CancellationToken ct)=>throw new Fe05NotificationUnavailableException("No approved runtime lease");}
    // Explicit in-memory store for orchestration tests; never described as SQL persistence evidence.
    private sealed class MemoryStore:IFe05OperationStore
    {
        public Dictionary<(long,Guid),Fe05StoredOperation> Operations=[];public Dictionary<Guid,Fe05NotificationCandidate> Events=[];
        public bool FailSecondWrite;public int Verifications;
        public Task RequireAvailableAsync(CancellationToken ct)=>Task.CompletedTask;
        public Task<Fe05StoredOperation?> LockAsync(long a,Guid o,CancellationToken ct)=>Task.FromResult(Operations.GetValueOrDefault((a,o)));
        public Task ReserveAsync(long a,Guid o,string f,string domain,CancellationToken ct){Operations.Add((a,o),new(f,domain,false,null,null));return Task.CompletedTask;}
        public Task WritePublicationAsync(Fe05NotificationCandidate c,Guid p,CancellationToken ct)
        {if(FailSecondWrite&&Events.Count==1)throw new InvalidOperationException("test write failure");Events.Add(p,c);return Task.CompletedTask;}
        public Task CompleteAsync(long a,Guid o,string f,Fe05OperationReceipt r,CancellationToken ct)
        {Operations[(a,o)]=Operations[(a,o)] with {N01ReceiptJson=JsonSerializer.Serialize(r)};return Task.CompletedTask;}
        public void OwnerFinalize(long a,Guid o)=>Operations[(a,o)]=Operations[(a,o)] with {Completed=true,DomainResultJson="{\"testOwnerReceipt\":true}"};
        public Task VerifyAsync(long a,Guid o,string f,Fe05OperationReceipt r,CancellationToken ct)
        {
            Verifications++;
            if(!Operations.TryGetValue((a,o),out var s)||s.Fingerprint!=f||s.N01ReceiptJson!=JsonSerializer.Serialize(r)||
                r.Publications.Any(p=>!Events.TryGetValue(p.PublicationId,out var e)||p.Fingerprint!=e.Fingerprint||!p.RecipientIds.SequenceEqual(e.RecipientIds)))throw new NotificationConflictException();
            return Task.CompletedTask;
        }
    }
    private sealed class StubConnection:DbConnection
    {
        public int Commits,Commands;private ConnectionState state=ConnectionState.Closed;
        [AllowNull]public override string ConnectionString{get;set;}="";
        public override string Database=>"no-sql-stub";public override string DataSource=>"no-sql-stub";public override string ServerVersion=>"16.0";public override ConnectionState State=>state;
        public override void Open()=>state=ConnectionState.Open;public override void Close()=>state=ConnectionState.Closed;
        public override void ChangeDatabase(string name)=>throw new NotSupportedException();
        protected override DbCommand CreateDbCommand(){Commands++;throw new InvalidOperationException("SQL forbidden in stub test");}
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolation)=>new StubTransaction(this,isolation);
    }
    private sealed class StubTransaction(StubConnection connection,IsolationLevel isolation):DbTransaction
    {
        public override IsolationLevel IsolationLevel=>isolation;protected override DbConnection DbConnection=>connection;
        public override void Commit()=>connection.Commits++;public override void Rollback(){}
    }
}
