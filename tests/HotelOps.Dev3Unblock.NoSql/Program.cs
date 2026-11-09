using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Dependencies;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

if(args.Length!=0)
{
 if(args.Length!=3||args[0]!="--sql-placement")throw new ArgumentException("Use --sql-placement owned-fixture.json approved-central-lease.lock, or no args for NoSQL.");
 using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(45));
 await SqlPlacementProbe.Run(args[1],args[2],limit.Token);return;
}
var checks=new List<string>();
void Check(string name,Action run){run();checks.Add(name);}
void Require(bool value){if(!value)throw new Exception("Assertion failed");}
void Reject<T>(Action run)where T:Exception{try{run();}catch(T){return;}throw new Exception("Expected "+typeof(T).Name);}
var location=new Dev2PlacementGuard.Location(7,true,11,7,true,13,7,true,11);
Check("UC016 active Room/Floor/Area relation accepted",()=>Dev2PlacementGuard.Validate(7,location));
Check("UC016 inactive floor rejected",()=>Reject<ValidationException>(()=>Dev2PlacementGuard.Validate(7,location with{FloorActive=false})));
Check("UC016 inactive area rejected",()=>Reject<ValidationException>(()=>Dev2PlacementGuard.Validate(7,location with{AreaActive=false})));
Check("UC016 inactive room rejected",()=>Reject<ValidationException>(()=>Dev2PlacementGuard.Validate(7,location with{Active=false})));
Check("UC016 foreign branch rejected",()=>Reject<ValidationException>(()=>Dev2PlacementGuard.Validate(8,location)));
Check("UC016 foreign floor rejected",()=>Reject<ValidationException>(()=>Dev2PlacementGuard.Validate(7,location with{FloorBranchId=8})));
Check("UC016 wrong area parent rejected",()=>Reject<ValidationException>(()=>Dev2PlacementGuard.Validate(7,location with{AreaFloorId=12})));
Check("UC016 missing floor rejected",()=>Reject<ValidationException>(()=>Dev2PlacementGuard.Validate(7,location with{FloorActive=null,FloorBranchId=null})));
Check("UC016 standalone Area without Floor permitted by actual schema",()=>Dev2PlacementGuard.Validate(7,new(7,true,null,null,null,null,null,null,null)));
Check("UC016 nonpositive parent identity rejected",()=>Reject<ValidationException>(()=>Dev2PlacementGuard.Validate(7,location with{FloorId=0})));
using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=invalid;Database=unopened;Integrated Security=true").Options);
Check("UC016 no caller transaction cannot acquire placement lease",()=>Reject<InvalidOperationException>(()=>new Dev2PlacementGuard(db).LockBranchAsync(7,default).GetAwaiter().GetResult()));
Check("UC016 preflight without retained Branch lease cannot validate references",()=>Reject<InvalidOperationException>(()=>new Dev2PlacementGuard(db).ValidateReferencesAsync(7,1,null,default).GetAwaiter().GetResult()));
Check("UC016 cancellation before SQL",()=>Reject<OperationCanceledException>(()=>new Dev2PlacementGuard(db).LockBranchAsync(7,new(true)).GetAwaiter().GetResult()));
Check("NoSQL leaves physical connection closed and no tracked writes",()=>Require(db.Database.GetDbConnection().State==System.Data.ConnectionState.Closed&&!db.ChangeTracker.Entries().Any()));
Check("UC006 actual Maintenance assignment query excludes historical creator",()=>{
 var rows=new[]{new MaintenanceWork{MaintenanceWorkId=1,AssigneeId=7,CreatedBy=9},new MaintenanceWork{MaintenanceWorkId=2,AssigneeId=9,CreatedBy=7}}.AsQueryable();
 Require(Dev2DependencyQueries.AssignedWorks(rows,7).Single().MaintenanceWorkId==1);
});
Check("UC006 latest resolved cycle supersedes stale pending approval",()=>{
 var parent=new MaterialRequest{RequestId=1,BranchId=7,Status="PENDING_APPROVAL"};
 var rows=new[]{new MaterialRequestApprovalCycle{ApprovalCycleId=1,RequestId=1,AssignedApproverId=9,RevisionNo=1,Status="PENDING",Request=parent},new MaterialRequestApprovalCycle{ApprovalCycleId=2,RequestId=1,AssignedApproverId=10,RevisionNo=2,Status="APPROVED",Request=parent}}.AsQueryable();
 Require(!Dev2DependencyQueries.PendingApprovals(rows,9,7).Any());
});
Check("UC006 pending approval requires target, branch, parent state and current revision",()=>{
 var parent=new MaterialRequest{RequestId=1,BranchId=7,Status="PENDING_APPROVAL"};
 var rows=new[]{new MaterialRequestApprovalCycle{ApprovalCycleId=1,RequestId=1,AssignedApproverId=9,RevisionNo=1,Status="PENDING",Request=parent}}.AsQueryable();
 Require(Dev2DependencyQueries.PendingApprovals(rows,9,7).Count()==1 && !Dev2DependencyQueries.PendingApprovals(rows,9,8).Any());
 parent.Status="CANCELLED";Require(!Dev2DependencyQueries.PendingApprovals(rows,9,7).Any());
});
Check("UC006 current custody and designated receiver do not infer historical actor",()=>{
 Require(Dev2DependencyQueries.UserAssets(new[]{new Asset{CustodianUserId=9},new Asset{CustodianUserId=7}}.AsQueryable(),7).Count()==1);
 Require(Dev2DependencyQueries.PendingUserHandovers(new[]{new AssetHandover{ToUserId=9,CreatedBy=7}}.AsQueryable(),7).Count()==0);
 Require(Dev2DependencyQueries.Classify("ASSET_HANDOVER","PENDING",true)==DependencyClassification.BLOCKING);
});
Check("UC006 historical reporter and retired custody are not blocking",()=>Require(Dev2DependencyQueries.Classify("HISTORICAL_ACTOR","INCIDENT_REPORTER")==DependencyClassification.HISTORICAL && Dev2DependencyQueries.Classify("ASSET","RETIRED")==DependencyClassification.HISTORICAL));
Check("Unknown lifecycle and resolved-but-unreviewed work remain unresolved",()=>Require(Dev2DependencyQueries.Classify("MAINTENANCE","COMPLETED")==DependencyClassification.UNKNOWN_POLICY&&Dev2DependencyQueries.Classify("INCIDENT","RESOLVED")==DependencyClassification.UNKNOWN_POLICY&&Dev2DependencyQueries.Classify("ASSET","ALIEN")==DependencyClassification.UNKNOWN_POLICY));
Check("UC012/026 maintenance scope excludes cross-branch inconsistent parents",()=>{
 var rows=new[]{new MaintenanceWork{MaintenanceWorkId=1,Incident=new(){BranchId=7},Department=new(){BranchId=7}},new MaintenanceWork{MaintenanceWorkId=2,Incident=new(){BranchId=7},Department=new(){BranchId=8}}}.AsQueryable();
 Require(Dev2DependencyQueries.Maintenance(rows,7).Single().MaintenanceWorkId==1);
});
Check("UC012/022 PM scope requires one authoritative target",()=>{
 var rows=new[]{new PreventiveMaintenanceSchedule{ScheduleId=1,RoomId=3,Room=new(){BranchId=7}},new PreventiveMaintenanceSchedule{ScheduleId=2,RoomId=3,AreaId=4,Room=new(){BranchId=7},Area=new(){BranchId=7}}}.AsQueryable();
 Require(Dev2DependencyQueries.Schedules(rows,7).Single().ScheduleId==1);
});
Check("Snapshot canonical ordering supports deterministic retry/conflict detection",()=>{
 var a=new DependencyFact("ASSET",1,7,"RETIRED",DependencyClassification.HISTORICAL);var b=a with{RecordId=2};
 var x=Dev2DependencyReader.Canonical("ROOM",3,7,[a,b],["OWNER"]);var y=Dev2DependencyReader.Canonical("ROOM",3,7,[b,a,a],["OWNER","OWNER"]);
 Require(x.Fingerprint==y.Fingerprint&&!x.CanDeactivate);
 Require(x.Fingerprint!=Dev2DependencyReader.Canonical("ROOM",3,7,[a,b with{State="IN_USE"}],["OWNER"]).Fingerprint);
});
Check("Canonical snapshot rejects cross-branch rows",()=>Reject<DependencyScopeException>(()=>Dev2DependencyReader.Canonical("BRANCH",7,7,[new("ASSET",1,8,"IN_USE",DependencyClassification.UNKNOWN_POLICY)],[])));
Check("Production dependency service denies anonymous actor before queries",()=>{
 var reader=new Dev2DependencyReader(db,new NoActor(),new DeniedPermissions(),null!);
 Reject<DependencyScopeException>(()=>reader.ReadBranchAsync(7,default).GetAwaiter().GetResult());
});
Check("Production dependency service rejects branch denial before resource lookup",()=>{
 var reader=new Dev2DependencyReader(db,new Actor(),new DeniedPermissions(),null!);
 Reject<DependencyScopeException>(()=>reader.ReadRoomAsync(1,7,default).GetAwaiter().GetResult());
});
Check("Actual correlated SQL predicates translate without connection",()=>{
 var sql=Dev2DependencyQueries.PendingApprovals(db.MaterialRequestApprovalCycles.AsNoTracking(),9,7).ToQueryString();Require(sql.Contains("NOT EXISTS")&&sql.Contains("revision_no"));
 Require(Dev2DependencyQueries.Maintenance(db.MaintenanceWorks.AsNoTracking(),7).ToQueryString().Contains("branch_id"));
 Require(Dev2DependencyQueries.Schedules(db.PreventiveMaintenanceSchedules.AsNoTracking(),7).ToQueryString().Contains("schedule_id"));
 Require(Dev2DependencyQueries.RoomIncidents(db,7,3).ToQueryString().Contains("current_room_id"));
});
Check("FE12 facts require source-read permission before SQL",()=>{
 var adapter=new Fe12MaintenanceDependencyAdapter(db,new Actor(),new DeniedPermissions());
 Reject<DependencyScopeException>(()=>adapter.ReadFactsAsync(1,7,default).GetAwaiter().GetResult());
});
Check("FE12 cycle binding refuses an absent accepted cycle contract",()=>{
 var adapter=new Fe12MaintenanceDependencyAdapter(db,new Actor(),new GrantedPermissions());
 Reject<HotelOps.Api.Services.WorkTime.WorkTimeContractException>(()=>adapter.ReadAsync(1,7,13,default).GetAwaiter().GetResult());
});
Check("FE12 revalidation cannot reuse an absent transaction lease",()=>{
 var adapter=new Fe12MaintenanceDependencyAdapter(db,new Actor(),new GrantedPermissions());
 Reject<InvalidOperationException>(()=>adapter.RevalidateInCallerTransactionAsync(new(1,7,9,11,"IN_PROGRESS",true,"hash"),default).GetAwaiter().GetResult());
});
Check("FE12 invalid cycle rejected before any database read",()=>Reject<HotelOps.Api.Services.WorkTime.WorkTimeContractException>(()=>new Fe12MaintenanceDependencyAdapter(db,null!,null!).ReadAsync(1,7,0,default).GetAwaiter().GetResult()));
Check("FE12 cancellation preserved before authorization or data access",()=>Reject<OperationCanceledException>(()=>new Fe12MaintenanceDependencyAdapter(db,null!,null!).ReadAsync(1,7,13,new(true)).GetAwaiter().GetResult()));
Check("SQL probe cannot run without the approved central lease",()=>Reject<InvalidOperationException>(()=>SqlPlacementProbe.Run("unread-fixture.json","unapproved.lock",default).GetAwaiter().GetResult()));
Check("SQL probe lease failure occurs before configuration or SQL reads",()=>Reject<InvalidOperationException>(()=>SqlPlacementProbe.Run("unread-fixture.json",Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString(),"SQL_TRIAL_EXCLUSIVE.lock"),default).GetAwaiter().GetResult()));
Check("Final reread rejects a stale dependency snapshot",()=>{
 var a=Dev2DependencyReader.Canonical("BRANCH",7,7,[],["OWNER"]);
 var b=Dev2DependencyReader.Canonical("BRANCH",7,7,[new("INCIDENT",1,7,"OPEN",DependencyClassification.UNKNOWN_POLICY)],["OWNER"]);
 Reject<DependencySnapshotConflictException>(()=>Dev2DependencyReader.RequireUnchanged(a,b));
 Dev2DependencyReader.RequireUnchanged(a,a);Require(!a.CanDeactivate);
});
Check("Final reread cannot reuse a lease across resources or transactions",()=>{
 var reader=new Dev2DependencyReader(db,new Actor(),new DeniedPermissions(),null!);
 Reject<InvalidOperationException>(()=>reader.RevalidateInCallerTransactionAsync(Dev2DependencyReader.Canonical("ROOM",1,7,[],[]),default).GetAwaiter().GetResult());
});
Check("UC016 missing trusted writer authority rejects before SQL",()=>{
 Reject<Dev2PlacementDeniedException>(()=>new HotelOps.Api.Services.Assets.AssetService(db).AssignLocationAsync(1,new(){ActorUserId=9,RoomId=1},default).GetAwaiter().GetResult());
 Reject<Dev2PlacementDeniedException>(()=>new HotelOps.Api.Services.Incidents.IncidentService(db).CreateIncidentAsync(new(){BranchId=7,ReportedBy=9,IncidentTitle="Test",Description="Test",DiscoveredAt=DateTime.UtcNow,RoomId=1},default).GetAwaiter().GetResult());
 Require(db.Database.GetDbConnection().State==System.Data.ConnectionState.Closed);
});
Check("Three placement actions retain route and require cookie authorization plus CSRF",()=>{
 foreach(var item in new[]{(typeof(HotelOps.Api.Controllers.AssetsController),"AssignLocation"),(typeof(HotelOps.Api.Controllers.AssetsController),"TransferLocation"),(typeof(HotelOps.Api.Controllers.IncidentsController),"CreateIncident")}){
  var m=item.Item1.GetMethod(item.Item2)!;
  Require(m.IsDefined(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute),false)&&m.IsDefined(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute),false)&&m.IsDefined(typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute),false));
 }
});
checks.AddRange(await ExpansionChecks.RunAsync(db));
checks.AddRange(await WriterOrderChecks.RunAsync());
Console.WriteLine(JsonSerializer.Serialize(new{pass=true,tests=checks.Count,checks,sqlConnections=0,sqlConcurrency="NOT_RUN",qualification="Actual production validator and transaction guard tests; not SQL writer/concurrency acceptance."}));
sealed class NoActor:ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo user){user=default;return false;}}
sealed class Actor:ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo user){user=new(9,1,false);return true;}}
sealed class DeniedPermissions:IPermissionService
{
 public Task<PermissionEvaluation> EvaluateBranchAsync(string p,long b,CancellationToken ct)=>Task.FromResult(new PermissionEvaluation(PermissionDecision.Denied));
 public Task<PermissionEvaluation> EvaluateGlobalAsync(string p,CancellationToken ct)=>Task.FromResult(new PermissionEvaluation(PermissionDecision.Denied));
 public Task<PermissionEvaluation> EvaluateResourceAsync(string p,PermissionResource r,CancellationToken ct)=>Task.FromResult(new PermissionEvaluation(PermissionDecision.Denied));
}

sealed class GrantedPermissions:IPermissionService
{
 public Task<PermissionEvaluation> EvaluateBranchAsync(string p,long b,CancellationToken ct)=>Task.FromResult(new PermissionEvaluation(PermissionDecision.Granted));
 public Task<PermissionEvaluation> EvaluateGlobalAsync(string p,CancellationToken ct)=>EvaluateBranchAsync(p,1,ct);
 public Task<PermissionEvaluation> EvaluateResourceAsync(string p,PermissionResource r,CancellationToken ct)=>EvaluateBranchAsync(p,1,ct);
}
