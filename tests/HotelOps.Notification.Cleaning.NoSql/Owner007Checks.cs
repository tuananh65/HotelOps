using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

internal static class Owner007Checks
{
 public static async Task RunAsync()
 {
  var checks=new List<string>();
  void Pass(string name)=>checks.Add(name);
  void Reject(Action act,string name){try{act();}catch(Exception e)when(e is InvalidOperationException or NotificationContractUnavailableException){Pass(name);return;}throw new Exception("Unexpected acceptance: "+name);}
  async Task Deny(Func<Task> act,string name){try{await act();}catch(Exception e)when(e is InvalidOperationException or NotificationContractUnavailableException){Pass(name);return;}throw new Exception("Unexpected acceptance: "+name);}
  using var rsa=RSA.Create(3072);var keys=new Keys(rsa.ExportParameters(false));var now=new DateTimeOffset(2026,11,10,12,0,0,TimeSpan.Zero);var hash=new string('A',64);
  var c=new NotifWriter004BLease(NotifWriter004BContract.Version,"UNIT_TEST_NOT_OWNER_APPROVAL",NotifWriter004BContract.PolicyVersion,
   NotifWriter004AContract.Server,NotifWriter004AContract.Database,NotifWriter004AContract.DatabaseGuid,"HotelOps_DEV2_FullStack_Runtime","HotelOps_DEV2_FullStack_Runtime",new string('A',32),"SQL_LOGIN",
   "dbo.fs013_assign_with_evidence","CALLER_STATIC_DBO_CHAIN",Guid.NewGuid(),Guid.NewGuid(),"RuntimeOwner",now.AddMinutes(-2),now.AddMinutes(-1),now.AddMinutes(5),now.AddMinutes(10),1,
   hash,hash,hash,hash,NotifWriter004BContract.OriginalWriterSha256,new(){["dbo.fs013_assign_with_evidence"]=hash,["dbo.fs009_verify_assignment_guard"]=hash},["NATIVE_RECEIPT_VERIFY","INBOX_READ"],30);
  NotifOwner007SignedLease Sign(NotifWriter004BLease lease){var payload=JsonSerializer.Serialize(lease);var signature=Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(NotifWriter004BContract.Version+"\n"+payload),HashAlgorithmName.SHA256,RSASignaturePadding.Pss));return new(payload,[..new[]{"ProjectLeader","SecurityOwner","NotificationOwner","SchemaOwner","RuntimeOwner"}.Select(o=>new NotifOwner007Signature(o,signature))]);}
  var packet=Sign(c);var verified=NotifWriter004BContract.Verify(packet,keys,now);Pass("D08 ephemeral trusted test keys verify structure only");
  foreach(var (bad,name) in new (NotifWriter004BLease,string)[]{(c with{Version="old"},"version"),(c with{LeaseId=Guid.Empty},"missing lease"),(c with{LeaseId=c.RetiredLeaseId},"retired identity reuse"),(c with{ExpiresAt=now},"expiry"),(c with{OwnerHardExpiry=now},"hard expiry"),(c with{Issuer="configuration"},"issuer"),(c with{Login="sa"},"principal"),(c with{SidHex="wrong"},"SID"),(c with{ActualDatabaseGuid=Guid.NewGuid()},"actual GUID"),(c with{Server="other"},"server"),(c with{Database="other"},"database"),(c with{Module="dbo.other"},"module"),(c with{ScriptSha256="missing"},"script hash"),(c with{SourceSha256="missing"},"source hash"),(c with{PermissionScopeSha256="missing"},"permission scope"),(c with{ApprovalReference=""},"approval"),(c with{Uses=["BACKGROUND_WORKER"]},"unapproved use")})
   Reject(()=>NotifWriter004BContract.Verify(Sign(bad),keys,now),"D08 rejects "+name);
  Reject(()=>NotifWriter004BContract.Verify(packet,new ClosedNotifOwner007Keys(),now),"D08 default key authority closed");
  Reject(()=>NotifWriter004BContract.Verify(packet with{Payload=packet.Payload+" "},keys,now),"D08 signed payload tamper");
  Reject(()=>NotifWriter004BContract.Verify(packet with{Signatures=[]},keys,now),"D08 missing signatures");
  var copy=verified.Contract;copy.Uses[0]="BACKGROUND_WORKER";copy.ModuleSha256.Clear();if(verified.Contract.Uses[0]!="NATIVE_RECEIPT_VERIFY"||verified.Contract.ModuleSha256.Count!=2)throw new Exception("Mutable lease snapshot");Pass("D08 verified lease defensive snapshot");
  var raw=File.ReadAllBytes("src/HotelOps.Api/Services/Notifications/NotifWriter004AContract.cs");if(Convert.ToHexString(SHA256.HashData(raw))!=NotifWriter004BContract.OriginalWriterSha256)throw new Exception("Original writer changed");Pass("D08 original Writer004A byte hash unchanged");
  if(!Encoding.UTF8.GetString(raw).Contains("2026-11-10T11:44:09.1792768Z"))throw new Exception("Old expiry changed");Pass("D08 original historical HardExpiry unchanged");
  await using var connection=new Connection();await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
  var observer=new Authority(c,now);var hook=new CleaningCommonTargetAdmission(packet,keys,observer,new Clock(now));
  await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
  {
   var store=new CleaningAssignmentStore(db,new Clock(now),hook);await store.EnsureAsync(default);Pass("D05 actual store hook wired with existing physical transaction");
   await Deny(()=>new CleaningAssignmentStore(db,new Clock(now)).EnsureAsync(default),"D05 absent hook fails closed");
   observer.Revoked=true;await Deny(()=>store.EnsureAsync(default),"D08 revoked central lease rejects before SQL");observer.Revoked=false;
   observer.Mutate=o=>o with{SourceAuthorityVerified=false};await Deny(()=>store.EnsureAsync(default),"D05 source authority rejects");
   observer.Mutate=o=>o with{ActualSourceRevision="changed"};await Deny(()=>store.EnsureAsync(default),"D05 source revision rejects");observer.Mutate=o=>o;
   await Deny(()=>store.InsertAsync(null!,null!,null!,default),"D05 common target cannot fall back to EF graph insert");
   var observation=observer.Observe(connection,tx.GetDbTransaction());
   foreach(var (bad,name) in new (NotifOwner007Observation,string)[]{(observation with{ActualDatabaseGuid=Guid.NewGuid()},"wrong GUID same name"),(observation with{ActualDatabaseGuid=null},"missing GUID"),(observation with{MetadataVisible=false},"inaccessible metadata"),(observation with{GuidSource="service_broker_guid"},"broker substitute"),(observation with{ObservedAt=now.AddMinutes(-2)},"stale observation"),(observation with{Login="sa"},"principal"),(observation with{SidHex=new string('B',32)},"SID"),(observation with{ScriptSha256=new string('B',64)},"signed script mismatch"),(observation with{TransactionCount=2},"nested transaction"),(observation with{Transaction=null},"physical transaction mismatch"),(observation with{CallerOwnsTransaction=false},"caller ownership"),(observation with{OperationUniqueIndex=false},"catalog invariant"),(observation with{DirectGraphWritesDenied=false},"DENY absent"),(observation with{NativeModuleHashesMatch=false},"module mismatch"),(observation with{PublicationImmutableTrigger=false},"immutable trigger"),(observation with{SourceSha256=new string('B',64)},"source hash mismatch")})
    Reject(()=>CleaningCommonTargetAdmission.RequireObservation(c,bad,connection,tx.GetDbTransaction(),now),"D05 rejects "+name);
   await tx.RollbackAsync();
  }
  await hook.RequireRuntimeCleanAsync(db,"INBOX_READ",default);Pass("D05 runtime clean admission with explicit test authority");
  observer.Revoked=true;var runtime=new CleaningNotificationRuntime(db,new MemoryStore(),new TestActivation(),new TestAuthority(),new Clock(now),hook);
  await Deny(()=>runtime.ListAsync(31,1,10,default),"D05 runtime RequireClean wired; revoked lease blocks query");observer.Revoked=false;
  await Deny(()=>new CleaningNotificationRuntime(db,new MemoryStore(),new TestActivation(),new TestAuthority(),new Clock(now)).ListAsync(31,1,10,default),"D05 runtime absent hook closed");
  await Deny(()=>new CleaningCommonTargetAdmission(packet,keys,new ClosedNotifOwner007RuntimeAuthority(),new Clock(now)).RequireRuntimeCleanAsync(db,"INBOX_READ",default),"D05 default observer closed");
  await Deny(()=>hook.RequireRuntimeCleanAsync(db,"CONTAINED_DELIVERY",default),"D08 lease scope rejects delivery");
  observer.Mutate=o=>{observer.Revoked=true;return o;};await Deny(()=>hook.RequireRuntimeCleanAsync(db,"INBOX_READ",default),"D08 revocation after observation rechecked");observer.Revoked=false;observer.Mutate=o=>o;
  observer.Mutate=o=>o with{ActualDatabaseGuid=Guid.NewGuid()};await Deny(()=>hook.RequireRuntimeCleanAsync(db,"INBOX_READ",default),"D05 integrated observer GUID mismatch rejects");observer.Mutate=o=>o;
  await using(var historical=new Connection{TargetServer=IntegrationSqlGuard.Server,TargetDatabase=IntegrationSqlGuard.Database})
  await using(var oldDb=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(historical).Options))
  await using(var oldTx=await oldDb.Database.BeginTransactionAsync(IsolationLevel.Serializable))
  {
   try{await new CleaningAssignmentStore(oldDb,new Clock(now),hook).EnsureAsync(default);throw new Exception("Historical query skipped");}
   catch(Exception e)when(e.Message=="SQL forbidden"){}
   if(historical.Commands!=1||historical.Opens!=0)throw new Exception("Historical guard bypass or extra open");Pass("D05 historical target retains original SQL guard instead of common-target hook (command blocked by fake)");
   await oldTx.RollbackAsync();
  }
  if(connection.Commands!=0||connection.Opens!=0)throw new Exception("Unexpected SQL/open");Pass("D05/D08 zero commands and connection opens");
  Console.WriteLine(JsonSerializer.Serialize(new{suite="Owner007",tests=checks.Count,passed=checks.Count,failed=0,sqlConnections=0,checks,qualification="Ephemeral signing keys; explicit observer and connection doubles. Production acceptance NOT_RUN."}));
 }
 private sealed class Keys(RSAParameters publicKey):INotifOwner007KeyPolicy{public RSA GetAcceptedKey(string owner){var r=RSA.Create();r.ImportParameters(publicKey);return r;}}
 private sealed class Clock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
 private sealed class Authority(NotifWriter004BLease c,DateTimeOffset now):INotifOwner007RuntimeAuthority
 {
  public bool Revoked;public Func<NotifOwner007Observation,NotifOwner007Observation> Mutate=o=>o;
  public Task RequireCurrentLeaseAsync(VerifiedNotifWriter004BLease lease,string use,CancellationToken ct){if(Revoked)throw new NotificationContractUnavailableException();return Task.CompletedTask;}
  public NotifOwner007Observation Observe(DbConnection connection,DbTransaction? tx)=>new(connection,tx,c.Server,c.Database,c.ActualDatabaseGuid,"sys.database_recovery_status.database_guid",true,now,c.Login,c.DatabaseUser,c.SidHex,true,true,c.SourceSha256,c.ScriptSha256,c.PermissionScopeSha256,c.CatalogSha256,true,true,true,true,true,true,true,true,"1","1",tx is null?0:1,tx is not null);
  public Task<NotifOwner007Observation> ObserveExistingAsync(VerifiedNotifWriter004BLease lease,DbConnection connection,DbTransaction? transaction,CancellationToken ct)=>Task.FromResult(Mutate(Observe(connection,transaction)));
 }
 private sealed class Connection:DbConnection
 {
  public int Commands,Opens;[AllowNull] public override string ConnectionString{get;set;}="";
  public string TargetDatabase=NotifWriter004AContract.Database,TargetServer=NotifWriter004AContract.Server;
  public override string Database=>TargetDatabase;public override string DataSource=>TargetServer;public override string ServerVersion=>"16.0";public override ConnectionState State=>ConnectionState.Open;
  public override void Open(){Opens++;throw new Exception("Open forbidden");}public override void Close(){}public override void ChangeDatabase(string name)=>throw new NotSupportedException();
  protected override DbCommand CreateDbCommand(){Commands++;throw new Exception("SQL forbidden");}
  protected override DbTransaction BeginDbTransaction(IsolationLevel level)=>new Transaction(this,level);
 }
 private sealed class Transaction(Connection connection,IsolationLevel level):DbTransaction{public override IsolationLevel IsolationLevel=>level;protected override DbConnection DbConnection=>connection;public override void Commit()=>throw new Exception("Commit forbidden");public override void Rollback(){}}
}
