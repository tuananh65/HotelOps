using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Optional real-SQL component only. No DEV1 writer invocation, DDL, fixture creation or commit.
internal static class CleaningSqlProbe
{
 internal sealed record Fixture(string Prefix,CleaningAssignmentCommand Command,CleaningAssignmentReceipt OwnerReceipt);
 internal static void RequireLease(string lease)
 {
  if(Path.GetFileName(lease)!="SQL_TRIAL_EXCLUSIVE.lock"||!File.Exists(lease))throw new InvalidOperationException("Approved central lease required.");
  try{using var probe=File.Open(lease,FileMode.Open,FileAccess.ReadWrite,FileShare.None);throw new InvalidOperationException("Central exclusive handle not held.");}
  catch(IOException e)when((e.HResult&0xffff)is 32 or 33){}
 }
 internal static async Task RunAsync(string fixtureFile,string lease)
 {
  RequireLease(lease);using var budget=new CancellationTokenSource(TimeSpan.FromMinutes(2));var ct=budget.Token;
  var f=JsonSerializer.Deserialize<Fixture>(await File.ReadAllTextAsync(fixtureFile,ct))??throw new ArgumentException("Fixture required");
  if(f.Prefix.Length is <13 or >20||!f.Prefix.StartsWith("F04P0-",StringComparison.Ordinal)||f.Prefix.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Owned F04P0- fixture prefix required");
  CleaningAssignmentProfile.ValidateCommand(f.Command);
  var config=new ConfigurationBuilder().AddUserSecrets("HotelOps.Dev2.FullStack.Trial.Integration02").Build();
  var connection=IntegrationSqlGuard.ValidateConnectionString(config.GetConnectionString("HotelOpsDb"));
  await using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).AddInterceptors(new IntegrationTrialConnectionInterceptor()).Options);
  await db.Database.OpenConnectionAsync(ct);await IntegrationSqlGuard.VerifyOpenConnectionAsync(db.Database.GetDbConnection(),ct);
  var inserted=false;
  await using(var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct))
  {
   try
   {
    RequireLease(lease);var store=new CleaningAssignmentStore(db,TimeProvider.System);await store.EnsureAsync(ct);
    if(!await db.Branches.AsNoTracking().AnyAsync(b=>b.BranchId==f.Command.BranchId&&b.BranchCode.StartsWith(f.Prefix),ct)||
       !await db.CleaningTasks.AsNoTracking().AnyAsync(t=>t.CleaningTaskId==f.Command.TaskId&&t.TaskNo.StartsWith(f.Prefix)&&t.Room.RoomNumber.StartsWith(f.Prefix),ct))throw new InvalidOperationException("Owned task/room/branch required");
    foreach(var id in new[]{f.Command.ActorId,f.Command.AssigneeId})
      if(!await db.Users.AsNoTracking().AnyAsync(u=>u.UserId==id&&u.Username.StartsWith(f.Prefix),ct))throw new InvalidOperationException("Owned actors required");
    var s=await store.SourceAsync(f.OwnerReceipt.AssignmentId,ct)??throw new InvalidOperationException("Owner must prepare persisted assignment fixture");
    var payload=CleaningAssignmentProfile.Build(f.Command,s,f.OwnerReceipt,DateTime.UtcNow);
    if(!await store.CanReadAsync(s,s.AssigneeId,ct))throw new UnauthorizedAccessException("Actual recipient source-read denied");
    var prior=await store.LockOperationAsync(f.Command.ActorId,f.Command.OperationId,ct);
    if(prior is null){await store.InsertAsync(f.Command,payload,f.OwnerReceipt,ct);inserted=true;}
    var graph=await store.GraphAsync(f.Command.ActorId,f.Command.OperationId,ct);
    var observed=CleaningAssignmentProfile.Verify(graph,f.Command);
    CleaningAssignmentProfile.VerifySource(observed,s,true);
    if(CleaningAssignmentProfile.PayloadHash(observed)!=CleaningAssignmentProfile.PayloadHash(payload))throw new NotificationConflictException();
   }
   finally{await tx.RollbackAsync(CancellationToken.None);}
  }
  if(inserted&&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WHERE actor_user_id={f.Command.ActorId} AND operation_key={f.Command.OperationId}").SingleAsync(ct)!=0)throw new InvalidOperationException("Rollback left operation rows");
  if(inserted&&(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_publications WHERE publication_id={f.OwnerReceipt.PublicationId}").SingleAsync(ct)!=0||
      await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_delivery_intents WHERE publication_id={f.OwnerReceipt.PublicationId}").SingleAsync(ct)!=0))throw new InvalidOperationException("Rollback left graph rows");
  Console.WriteLine(JsonSerializer.Serialize(new{pass=true,scope="Owned SQL graph/source-read component only",inserted,rollback="PASS",domainWrites=0,ownerFinalCommit="NOT_RUN",concurrency="NOT_RUN",deliveryInbox="NOT_RUN"}));
 }
}
