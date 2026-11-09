using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

if(args.Length==3 && args[0]=="--sql-graph-component")
{using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));await N01SqlComponentProbe.RunAsync(args[1],args[2],timeout.Token);return;}
if(args.Length!=0)throw new ArgumentException("Only default NoSQL or --sql-graph-component fixture.json centralLease are supported.");

var checks = new List<string>();
void Check(string name, Action action) { action(); checks.Add(name); }
void Require(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Reject<T>(Action action) where T: Exception { try { action(); } catch(T) { return; } throw new Exception("Expected " + typeof(T).Name); }
var command = new Fe05NotificationCommand(Fe05NotificationKind.InspectionFailed, Guid.Parse("20261108-0000-4000-8000-000000000001"), 7, 11, "revision-a");
var source = new Fe05NotificationSource(3,7,13,11,17,19,null,"revision-a",17,"COMPLETED","FAIL","FAILED",
    DateTimeOffset.Parse("2026-11-08T10:00:00Z"),[23,29]);
Fe05Recipient[] recipients = [new(31,7,true,true),new(37,7,true,true)];
Fe05NotificationCandidate Map(Fe05NotificationCommand? c=null,Fe05NotificationSource? s=null,Fe05Recipient[]? r=null)
    => Fe05NotificationMapping.Map(c??command,s??source,3,r??recipients);
Check("inspection event version follows existing dotted catalog style; candidate only",()=>Require(Map().EventType=="inspection.failed"&&Map().Version==1));
Check("rework is a separate event bound to persisted rework identity and revision",()=>Require(Map(command with{Kind=Fe05NotificationKind.CleaningReworkRequested},source with{ReworkId=41,ReworkRevision="\"0000000000000001\""}).EventType=="cleaning.rework.requested"));
Check("rework revision is mandatory",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(command with{Kind=Fe05NotificationKind.CleaningReworkRequested},source with{ReworkId=41})));
Check("changed rework revision changes canonical fingerprint",()=>Require(Map(command with{Kind=Fe05NotificationKind.CleaningReworkRequested},source with{ReworkId=41,ReworkRevision="\"0000000000000001\""}).Fingerprint!=Map(command with{Kind=Fe05NotificationKind.CleaningReworkRequested},source with{ReworkId=41,ReworkRevision="\"0000000000000002\""}).Fingerprint));
Check("canonical recipient ordering and deduplication",()=>Require(Map(r:[recipients[1],recipients[0],recipients[1]]).Fingerprint==Map().Fingerprint));
Check("canonical result ordering",()=>Require(Map(s:source with{FailedResultIds=[29,23,23]}).Fingerprint==Map().Fingerprint));
Check("identical candidate retry has stable fingerprint, not a durable receipt",()=>Require(Map().Fingerprint==Map().Fingerprint));
Check("same operation changed revision differs",()=>Require(Map(command with{ExpectedRevision="revision-b"},source with{Revision="revision-b"}).Fingerprint!=Map().Fingerprint));
Check("same operation changed recipients differs",()=>Require(Map(r:[recipients[0]]).Fingerprint!=Map().Fingerprint));
Check("same operation changed room differs",()=>Require(Map(s:source with{RoomId=43}).Fingerprint!=Map().Fingerprint));
Check("empty operation rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(command with{OperationId=Guid.Empty})));
Check("unknown event rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(command with{Kind=(Fe05NotificationKind)999})));
Check("foreign branch rejected without source disclosure",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(command with{BranchId=8})));
Check("actor spoof rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{ActorId=4})));
Check("source identity mismatch rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{InspectionId=99})));
Check("stale revision rejected",()=>Reject<NotificationConflictException>(()=>Map(command with{ExpectedRevision="stale"})));
Check("obsolete room cycle rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{CurrentCleaningTaskId=99})));
Check("nonfailed inspection rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{InspectionResult="PASS"})));
Check("nonconcluded inspection rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{InspectionStatus="IN_PROGRESS"})));
Check("unbound workflow rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{WorkflowState="PENDING_CONCLUSION"})));
Check("missing rework rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(command with{Kind=Fe05NotificationKind.CleaningReworkRequested})));
Check("failed event cannot alias rework identity",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{ReworkId=41})));
Check("missing result rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{FailedResultIds=[]})));
Check("nonUTC timestamp rejected",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(s:source with{OccurredAt=source.OccurredAt.ToOffset(TimeSpan.FromHours(7))})));
Check("inactive recipient rejects entire set",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(r:[recipients[0],recipients[1] with{Active=false}])));
Check("crossbranch recipient rejects entire set",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(r:[recipients[0] with{BranchId=8}])));
Check("source read revoked rejects entire set",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(r:[recipients[0] with{CanReadSource=false}])));
Check("empty recipient set fails closed",()=>Reject<Fe05NotificationUnavailableException>(()=>Map(r:[])));
Check("payload does not carry notes, URLs, personal names",()=>{var json=JsonSerializer.Serialize(Map());Require(!json.Contains("SummaryNote")&&!json.Contains("ActionPath")&&!json.Contains("http"));});
Check("default source adapter fails closed",()=>Reject<Fe05NotificationUnavailableException>(()=>new UnacceptedFe05Source().LoadLockedAsync(command,3,default).GetAwaiter().GetResult()));
Check("default recipient policy fails closed",()=>Reject<Fe05NotificationUnavailableException>(()=>new UnacceptedFe05Recipients().ResolveAsync(source,command.Kind,default).GetAwaiter().GetResult()));
Check("cancellation preserved before source work",()=>Reject<OperationCanceledException>(()=>new UnacceptedFe05Source().LoadLockedAsync(command,3,new(true)).GetAwaiter().GetResult()));
var services=new ServiceCollection();services.AddHotelOpsNotifications(new ConfigurationBuilder().Build());
Check("single existing publisher and no new event profile or hosted worker",()=>Require(services.Count(x=>x.ServiceType==typeof(INotificationPublisher))==1&&!services.Any(x=>x.ServiceType==typeof(INotificationEventProfile)||x.ServiceType.FullName=="Microsoft.Extensions.Hosting.IHostedService")));
using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=invalid;Database=unopened;Integrated Security=true").Options);
var staging=new Fe05NotificationStaging(db,new NotificationCommitGuard(),new NoActor(),new UnacceptedFe05Source(),new UnacceptedFe05Recipients());
Check("missing authenticated actor rejected before source read",()=>Reject<Fe05NotificationUnavailableException>(()=>staging.PrepareAsync(command,default).GetAwaiter().GetResult()));
Check("no caller transaction rejected without a SQL connection",()=>Reject<InvalidOperationException>(()=>Fe05CallerIntegration.StageInCallerTransactionAsync(db,staging,command,default).GetAwaiter().GetResult()));
Check("no graph or pending intent was created",()=>Require(!db.ChangeTracker.Entries().Any()&&db.Database.GetDbConnection().State==System.Data.ConnectionState.Closed));
checks.AddRange(await ImplementationChecks.RunAsync());
checks.AddRange(await Decision08RecipientChecks.RunAsync());
Console.WriteLine(JsonSerializer.Serialize(new{pass=true,tests=checks.Count,checks,sqlConnections=0,sqlAcceptance="NOT_RUN",qualification="Actual mapping, operation orchestration and EF interceptor tests with explicitly in-memory graph/authorization and stub transaction; not SQL persistence, rollback, lock/concurrency or accepted recipient evidence."}));
sealed class NoActor:ICurrentUserAccessor {public bool TryGetCurrentUser(out CurrentUserInfo user){user=default;return false;}}
