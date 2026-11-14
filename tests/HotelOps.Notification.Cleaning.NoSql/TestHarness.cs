using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class Fixtures
{
 public static readonly DateTime Now=new(2026,11,9,5,0,0,DateTimeKind.Utc);
 public static CleaningAssignmentCommand Command=>new(3,7,17,31,Guid.Parse("20261109-0000-4000-8000-000000000029"),new string('A',64));
 public static CleaningAssignmentReceipt Receipt=>new(17,23,7,31,Command.OperationId,Guid.Parse("20261109-0000-4000-8000-000000000129"),"ASSIGNED","PENDING_DELIVERY","\"0000000000000001\"");
 public static CleaningAssignmentState State()=>new(){AssignmentId=23,TaskId=17,RoomId=13,BranchId=7,ActorId=3,AssigneeId=31,
   AssignedAt=Now.AddMinutes(-5),AssignmentStatus="ACTIVE",WorkflowState="ASSIGNED",TaskStatus="PENDING",DueAt=Now.AddHours(1),
   CurrentCycleId=17,Revision=[0,0,0,0,0,0,0,1],ActiveAssignments=1,LocationActive=true};
 public static CleaningAssignmentGraph Graph(CleaningAssignmentEvent e)=>new(3,Command.OperationId,new string('A',64),1,true,
   JsonSerializer.Serialize(Receipt,CleaningAssignmentProfile.Json),Receipt.PublicationId,CleaningAssignmentProfile.EventType,1,23,
   [0,0,0,0,0,0,0,1],JsonSerializer.Serialize(e,CleaningAssignmentProfile.Json),true,[31],1);
}
internal sealed class MemoryStore:ICleaningAssignmentStore
{
 public CleaningAssignmentState State=Fixtures.State();public bool Readable=true;public int Inserts;
 public CleaningStoredOperation? Operation;public CleaningAssignmentGraph? Graph;
 public Task EnsureAsync(CancellationToken ct)=>Task.CompletedTask;
 public Task<CleaningStoredOperation?> LockOperationAsync(long actor,Guid op,CancellationToken ct)=>Task.FromResult(Operation);
 public Task<CleaningAssignmentState?> SourceAsync(long id,CancellationToken ct)=>Task.FromResult<CleaningAssignmentState?>(id==State.AssignmentId?State:null);
 public Task<CleaningAssignmentGraph> GraphAsync(long actor,Guid op,CancellationToken ct)=>Task.FromResult(Graph??throw new NotificationConflictException());
 public Task<bool> CanReadAsync(CleaningAssignmentState s,long user,CancellationToken ct)=>Task.FromResult(Readable);
 public Task InsertAsync(CleaningAssignmentCommand c,CleaningAssignmentEvent p,CleaningAssignmentReceipt r,CancellationToken ct)
 {Inserts++;Graph=Fixtures.Graph(p);Operation=new(c.RequestFingerprint,true,Graph.ResultJson);return Task.CompletedTask;}
 public void SeedNative(){var e=CleaningAssignmentProfile.Build(Fixtures.Command,State,Fixtures.Receipt,Fixtures.Now);Graph=Fixtures.Graph(e);Operation=new(Fixtures.Command.RequestFingerprint,true,Graph.ResultJson);}
}
internal sealed class TestAuthority:ICleaningActorAuthority
{public Task RequireWriterAsync(CleaningAssignmentCommand c,CancellationToken ct)=>Task.CompletedTask;public Task RequireViewerAsync(long u,CancellationToken ct)=>Task.CompletedTask;}
internal sealed class TestActivation:ICleaningNotificationActivation
{public Task RequireAsync(CleaningNotificationCapability c,CancellationToken ct)=>Task.CompletedTask;}
internal sealed class FixedClock:TimeProvider{public override DateTimeOffset GetUtcNow()=>new(Fixtures.Now);}
internal sealed class TestHarness:IAsyncDisposable
{
 public StubConnection Connection{get;}=new();public NotificationCommitGuard Guard{get;}=new();
 public HotelOpsDbContext Db{get;}public IDbContextTransaction Tx{get;private set;}=null!;public MemoryStore Store{get;}
 public CleaningNotificationParticipant Service{get;}
 private TestHarness(MemoryStore s){Store=s;Db=new(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(Connection).AddInterceptors(Guard).Options);Service=new(Db,Guard,new TestActivation(),new TestAuthority(),s,new FixedClock());}
 public static async Task<TestHarness> Open(MemoryStore s){var h=new TestHarness(s);h.Tx=await h.Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);return h;}
 public Task<CleaningAssignmentLease> Enter(CleaningAssignmentCommand c)=>Service.EnterAsync(Connection,Tx.GetDbTransaction(),c,default);
 public Task<CleaningAssignmentReceipt> Stage(CleaningAssignmentLease lease)=>Service.StageAsync(Connection,Tx.GetDbTransaction(),lease,23,Fixtures.Receipt,default);
 public async ValueTask DisposeAsync(){await Tx.DisposeAsync();await Db.DisposeAsync();}
}
internal sealed class StubConnection:DbConnection
{
 public int Commits,Commands;private ConnectionState state=ConnectionState.Closed;
 [AllowNull]public override string ConnectionString{get;set;}="";
 public override string Database=>"NoSQL";public override string DataSource=>"NoSQL";public override string ServerVersion=>"16.0";public override ConnectionState State=>state;
 public override void Open()=>state=ConnectionState.Open;public override void Close()=>state=ConnectionState.Closed;
 public override void ChangeDatabase(string name)=>throw new NotSupportedException();
 protected override DbCommand CreateDbCommand(){Commands++;throw new InvalidOperationException("SQL forbidden in stub");}
 protected override DbTransaction BeginDbTransaction(IsolationLevel level)=>new StubTransaction(this,level);
}
internal sealed class StubTransaction(StubConnection connection,IsolationLevel level):DbTransaction
{public override IsolationLevel IsolationLevel=>level;protected override DbConnection DbConnection=>connection;public override void Commit()=>connection.Commits++;public override void Rollback(){}}
