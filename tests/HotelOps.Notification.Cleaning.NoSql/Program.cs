using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

if(args.Length==3&&args[0]=="--sql-component"){await CleaningSqlProbe.RunAsync(args[1],args[2]);return;}
if(args.Length!=0)throw new ArgumentException("Use default NoSQL or --sql-component fixture.json centralLease");
var checks=new List<string>();
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks.Add(name);}
async Task Deny<T>(Func<Task> action,string name)where T:Exception
{try{await action();}catch(T){checks.Add(name);return;}throw new Exception("Expected "+typeof(T).Name+": "+name);}
var c=Fixtures.Command;var s=Fixtures.State();var r=Fixtures.Receipt;
var e=CleaningAssignmentProfile.Build(c,s,r,Fixtures.Now);
var g=Fixtures.Graph(e);
Check(CleaningAssignmentProfile.EventType=="CLEANING_TASK_ASSIGNED"&&CleaningAssignmentProfile.Version==1,"Exact FS020 CLEANING_TASK_ASSIGNED/v1");
Check(CleaningAssignmentProfile.ReadPolicy=="cleaning.assignment.view.v1","Existing CLEANING_VIEW source policy binding retained");
Check(CleaningAssignmentProfile.ActionPath(s)=="/cleaning/tasks/17?branchId=7&assignmentId=23","Exact persisted assignment-specific action path");
Check(CleaningAssignmentProfile.Verify(g,c)==e,"Existing FS013 payload/result shapes verify");
Check(CleaningAssignmentProfile.PayloadHash(e)==CleaningAssignmentProfile.PayloadHash(e with{}),"Canonical retry fingerprint stable");
Check(CleaningAssignmentProfile.PayloadHash(e)!=CleaningAssignmentProfile.PayloadHash(e with{AssignmentId=24}),"Different persisted assignment changes fingerprint");
Check(CleaningAssignmentProfile.PayloadHash(e)!=CleaningAssignmentProfile.PayloadHash(e with{AssigneeUserId=32}),"Old recipient cannot be retargeted");
var native=JsonSerializer.Serialize(e with{AssignedAtUtc=DateTime.SpecifyKind(e.AssignedAtUtc,DateTimeKind.Unspecified),DueAtUtc=DateTime.SpecifyKind(e.DueAtUtc,DateTimeKind.Unspecified)},CleaningAssignmentProfile.Json);
Check(CleaningAssignmentProfile.Verify(g with{PayloadJson=native},c)==e,"Native SQL datetime2 JSON normalized as documented UTC");
foreach(var (name,change)in new (string,Action<CleaningAssignmentState>)[]{
 ("foreign branch",x=>x.BranchId=8),("wrong task",x=>x.TaskId=99),("wrong assignee",x=>x.AssigneeId=99),
 ("wrong actor",x=>x.ActorId=99),("missing assignment",x=>x.AssignmentId=0),("inactive parent",x=>x.LocationActive=false),
 ("old cycle",x=>x.CurrentCycleId=99),("ambiguous active assignment",x=>x.ActiveAssignments=2),
 ("cancelled assignment",x=>x.AssignmentStatus="CANCELLED"),("superseded assignment",x=>x.EndedAt=Fixtures.Now),
 ("cancelled task",x=>x.WorkflowState="CANCELLED"),("not ASSIGNED transition",x=>x.WorkflowState="IN_PROGRESS"),
 ("missing revision",x=>x.Revision=[]),("future assigned time",x=>x.AssignedAt=Fixtures.Now.AddDays(1))})
 await Deny<Exception>(()=>{var bad=Fixtures.State();change(bad);return Task.FromResult(CleaningAssignmentProfile.Build(c,bad,r,Fixtures.Now));},"Mapping rejects "+name);
foreach(var (name,changed)in new (string,CleaningAssignmentGraph)[]{
 ("missing mandatory intent",g with{Recipients=[]}),("extra mandatory intent",g with{Recipients=[31,32]}),
 ("wrong recipient",g with{Recipients=[32]}),("missing audit",g with{HasAudit=false}),
 ("incomplete operation",g with{Completed=false}),("foreign fingerprint",g with{Fingerprint=new string('B',64)}),
 ("second graph publication",g with{OperationPublications=2}),("wrong event",g with{EventType="invented"}),
 ("changed payload identity",g with{PayloadJson=JsonSerializer.Serialize(e with{RoomId=99},CleaningAssignmentProfile.Json),ResultJson=g.ResultJson})})
{
 if(name=="changed payload identity")continue; // current source comparison below supplies authoritative RoomId
 await Deny<NotificationConflictException>(()=>Task.FromResult(CleaningAssignmentProfile.Verify(changed,c)),"Graph rejects "+name);
}
await Deny<NotificationConflictException>(()=>{CleaningAssignmentProfile.VerifySource(e with{RoomId=99},s,false);return Task.CompletedTask;},"Publication room bound to authoritative task source");
Check(CleaningAssignmentProfile.Current(s,false)==CleaningSourceDisposition.Denied,"Recipient permission denial distinct from stale source");
var started=Fixtures.State();started.WorkflowState="IN_PROGRESS";started.TaskStatus="IN_PROGRESS";started.Revision=[0,0,0,0,0,0,0,2];
CleaningAssignmentProfile.VerifySource(e,started,false);checks.Add("Legitimate Start/IN_PROGRESS does not obsolete assignment notification");
await Deny<CleaningSourceStaleException>(()=>{CleaningAssignmentProfile.VerifySource(e,started,true);return Task.CompletedTask;},"Stage still requires exact transition revision");
await Deny<NotificationContractUnavailableException>(()=>new CleaningNotificationDisabled().RequireAsync(CleaningNotificationCapability.Staging,default),"Default producer activation closed");
await Deny<NotificationContractUnavailableException>(()=>new CleaningNotificationDisabled().RequireAsync(CleaningNotificationCapability.ContainedDelivery,default),"Default shared delivery remains OFF");
await Deny<OperationCanceledException>(()=>new CleaningNotificationDisabled().RequireAsync(CleaningNotificationCapability.Inbox,new(true)),"Activation preserves cancellation");
var store=new MemoryStore();
await using(var h=await TestHarness.Open(store))
{
 var lease=await h.Enter(c);Check(store.Inserts==0,"Enter retains operation range without duplicate reservation insert");
 await h.Stage(lease);Check(store.Inserts==1,"EF path creates one existing FS013 graph");
 await h.Tx.CommitAsync();Check(h.Connection.Commits==1,"Actual EF async commit executes graph/source verifier");
 Check(!h.Db.ChangeTracker.Entries().Any(),"No C04 intent or second tracked graph");
}
await using(var h=await TestHarness.Open(store))
{
 var lease=await h.Enter(c);Check(lease.Replay==r&&store.Inserts==1,"Identical replay returns existing receipt before mutation");
 await h.Tx.CommitAsync();Check(store.Inserts==1,"Replay creates no publication or retargeting");
}
await using(var h=await TestHarness.Open(store))
{
 await Deny<NotificationConflictException>(()=>h.Enter(c with{RequestFingerprint=new string('B',64)}),"Conflicting operation rejected before mutation");
 await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Caught conflict poisons actual EF commit");
}
await using(var h=await TestHarness.Open(new MemoryStore()))
{
 var lease=await h.Enter(c);
 await Deny<InvalidOperationException>(()=>h.Guard.VerifyBeforeCommitAsync(h.Db,default),"Unstaged mandatory lease blocks EF commit guard");
 await h.Stage(lease);
 await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Caught premature mandatory verification cannot be healed into commit");
}
await using(var h=await TestHarness.Open(new MemoryStore()))
{
 var lease=await h.Enter(c);await h.Stage(lease);_=await h.Enter(c);h.Store.State.EndedAt=Fixtures.Now;
 await Deny<CleaningSourceStaleException>(()=>h.Tx.CommitAsync(),"Same-transaction replay cannot replace fresh mutation source verifier");
}
await using(var h=await TestHarness.Open(new MemoryStore()))
{
 var lease=await h.Enter(c);h.Store.SeedNative();await h.Stage(lease);
 Check(h.Store.Inserts==0,"Native SQL graph adopted without second operation/publication/intent");await h.Tx.CommitAsync();
}
await using(var h=await TestHarness.Open(new MemoryStore()))
{
 var lease=await h.Enter(c);h.Store.Readable=false;
 await Deny<UnauthorizedAccessException>(()=>h.Stage(lease),"Recipient source-read denied before first insert");
 Check(h.Store.Inserts==0,"Denial inserts zero graphs");await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Recipient failure poison survives caller catch");
}
await using(var h=await TestHarness.Open(new MemoryStore()))
{
 var lease=await h.Enter(c);await h.Stage(lease);h.Store.State.EndedAt=Fixtures.Now;
 await Deny<CleaningSourceStaleException>(()=>h.Tx.CommitAsync(),"Supersession after staging rejected at actual commit");
 Check(h.Connection.Commits==0,"Superseded new mutation never reaches provider commit");
}
await using(var h=await TestHarness.Open(new MemoryStore()))
{
 var lease=await h.Enter(c);await h.Stage(lease);h.Store.Readable=false;
 await Deny<UnauthorizedAccessException>(()=>h.Tx.CommitAsync(),"Revoked permission rejected on commit reread");
}
await using(var h=await TestHarness.Open(new MemoryStore()))
await using(var other=await TestHarness.Open(new MemoryStore()))
{
 var lease=await h.Enter(c);await Deny<InvalidOperationException>(()=>other.Stage(lease),"Lease cannot cross context/physical transaction");
}
await using(var h=await TestHarness.Open(new MemoryStore()))
{
 var lease=await h.Enter(c);await Deny<OperationCanceledException>(()=>h.Service.StageAsync(h.Connection,h.Tx.GetDbTransaction(),lease,s.AssignmentId,r,new(true)),"Cancelled staging propagates");
 await Deny<InvalidOperationException>(()=>h.Tx.CommitAsync(),"Cancelled required stage poisons commit");
}
await using(var h=await TestHarness.Open(new MemoryStore()))
{
 var runtime=new CleaningNotificationRuntime(h.Db,h.Store,new CleaningNotificationDisabled(),new TestAuthority(),TimeProvider.System);
 await Deny<NotificationContractUnavailableException>(()=>runtime.RunContainedBatchAsync(default),"Concrete dispatcher refuses default gate before SQL");
 await Deny<NotificationContractUnavailableException>(()=>runtime.ListAsync(31,1,20,default),"Concrete inbox refuses default gate before SQL");
 await Deny<NotificationContractUnavailableException>(()=>runtime.GetAsync(31,r.PublicationId,default),"Concrete detail refuses default gate before SQL");
 await Deny<NotificationContractUnavailableException>(()=>runtime.SetReadAsync(31,r.PublicationId,new(true,Convert.ToBase64String(new byte[8])),default),"Concrete read command refuses default gate before SQL");
 Check(h.Connection.Commands==0,"Negative runtime paths issue zero SQL commands");
}
var services=new ServiceCollection();services.AddHotelOpsNotifications(new ConfigurationBuilder().Build());services.AddHotelOpsCleaningNotificationCompatibility();services.AddHotelOpsCleaningNotificationCompatibility();
Check(services.Count(x=>x.ServiceType==typeof(INotificationPublisher))==1,"Combined registration preserves one existing publisher");
Check(services.Count(x=>x.ServiceType==typeof(ICleaningNotificationRuntime))==1,"Cleaning explicit registration idempotent");
Check(!services.Any(x=>x.ServiceType==typeof(INotificationEventProfile)||x.ServiceType.FullName=="Microsoft.Extensions.Hosting.IHostedService"),"No C04 Cleaning profile or hosted worker introduced");
Check(services.Single(x=>x.ServiceType==typeof(IFe05NotificationSource)).ImplementationType==typeof(UnacceptedFe05Source),"N01 fail-closed source registration preserved");
Check(NotificationDeliveryFailures.IsRetryable(new NotificationDeliveryTransientException("fixture"))&&!NotificationDeliveryFailures.IsRetryable(new UnauthorizedAccessException()),"Transient retrieval differs from definitive denial");
Check(CleaningNotificationErrors.Classify(new NotificationConflictException())==CleaningNotificationFailure.Conflict,"Conflict error maps independently from unavailable graph");
Check(CleaningNotificationErrors.Classify(new UnauthorizedAccessException())==CleaningNotificationFailure.Forbidden,"Permission error maps to forbidden after rollback");
Check(CleaningNotificationErrors.Classify(new NotificationContractUnavailableException())==CleaningNotificationFailure.Unavailable,"Unaccepted graph maps unavailable");
Check(CleaningNotificationErrors.Classify(new OperationCanceledException())==CleaningNotificationFailure.Cancelled,"Cancellation keeps its own error outcome");
Check(CleaningNotificationErrors.Classify(new NotificationDeliveryTransientException("fixture"))==CleaningNotificationFailure.Unavailable,"Infrastructure failure does not become authorization denial");
await Deny<InvalidOperationException>(()=>Task.Run(()=>CleaningSqlProbe.RequireLease("missing/SQL_TRIAL_EXCLUSIVE.lock")),"SQL harness lease missing rejects before config/SQL");
Console.WriteLine(JsonSerializer.Serialize(new{pass=true,tests=checks.Count,checks,sqlConnections=0,qualification="Actual new profile, participant, EF commit interception, wiring and closed runtime tests; graph/auth/store/provider are explicit test doubles in positive orchestration checks. SQL persistence, delivery/inbox positives and concurrency NOT_RUN."}));
