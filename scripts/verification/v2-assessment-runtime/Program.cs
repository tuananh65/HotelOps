using System.Data;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

const string database="HotelOpsCompactWorkdayTestV22611250010";
var root=Directory.GetCurrentDirectory();
var settings=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json")));
var connection=new SqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings:HotelOpsDb").GetString()){InitialCatalog=database};
var options=new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection.ConnectionString).Options;
var results=new List<object>();
var failed=0;
async Task Test(string name,Func<Fixture,Task> action){
 await using var db=new HotelOpsCompactDbContext(options);
 await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
 var started=DateTime.UtcNow;
 try{
  var fixture=await Fixture.Create(db);
  await action(fixture);
  results.Add(new{name,status="PASS",milliseconds=(DateTime.UtcNow-started).TotalMilliseconds});
  Console.WriteLine("PASS "+name);
 }catch(Exception e){
  failed++;var error=e.ToString();results.Add(new{name,status="FAIL",error});Console.WriteLine("FAIL "+name+": "+error);
 }finally{await transaction.RollbackAsync();}
}
await Test("initial reservation is maximum room norm; assessment releases surplus",async f=>{
 await f.Reserve(0);var r=await f.Reservation();Equal(r.Quantity,4m,"initial max");
 await f.Assess(3,0);r=await f.Reservation();Equal(r.Quantity,1m,"actual deficit");Equal((await f.Balance(0)).Reserved,1m,"reserved");
 Equal(await f.Db.StaffSupplyLedgers.Where(x=>x.BalanceId==f.BalanceIds[0]&&x.Kind=="RELEASE").SumAsync(x=>x.Quantity),3m,"released");
});
await Test("reassignment preserves assessed deficit and original per-guest snapshot",async f=>{
 await f.Reserve(0);await f.Assess(3,0);await f.Reserve(1);
 var r=await f.Reservation();Equal(r.Quantity,1m,"deficit");Equal(r.PerGuestQuantity,1m,"old norm");Equal((await f.Balance(1)).PerGuestQuantity,2m,"new staff norm differs");Equal((await f.Balance(0)).Reserved,0m,"old released");
 var snap=await f.Service.RoomAssessment(f.Work,default);Equal(snap.Single().RequiredQuantity,4m,"room norm snapshot");Equal(snap.Single().UsableQuantity,3m,"carried usable");
});
await Test("unassessed reassignment also retains old maximum norm",async f=>{
 await f.Reserve(0);await f.Reserve(1);var r=await f.Reservation();Equal(r.Quantity,4m,"not new norm eight");Equal(r.PerGuestQuantity,1m,"snapshot");
});
await Test("recorded partial refill is settled once on reassignment and batch debited exactly",async f=>{
 await f.Reserve(0);await f.Assess(1,1);var key=Guid.NewGuid();await f.Reserve(1,key);await f.Reserve(1,key);
 Equal((await f.Balance(0)).Used,1m,"old physical placement");Equal((await f.Balance(0)).Reserved,0m,"old released");Equal((await f.Reservation()).Quantity,2m,"remaining deficit");
 Equal(await f.Db.StaffBatchBalances.Where(h=>h.BalanceId==f.BalanceIds[0]).SumAsync(h=>h.Quantity),19m,"old batch debited once");
 Equal(await f.Db.StaffSupplyLedgers.CountAsync(l=>l.BalanceId==f.BalanceIds[0]&&l.Kind=="USE"),1,"use once");
 var snap=await f.Service.RoomAssessment(f.Work,default);Equal(snap.Single().CurrentQuantity,2m,"current now includes placed");Equal(snap.Single().ReplenishedQuantity,0m,"new staff starts zero");
});
await Test("new staff missing a prior reserved item is rejected before old holds change",async f=>{
 await f.Reserve(0);await f.Assess(3,0);await f.AddOldOnlyItem();
 var before=await f.Db.SupplyReservations.AsNoTracking().Where(r=>r.WorkId==f.Work&&r.Status=="RESERVED").Select(r=>new{r.ReservationId,r.Quantity,r.BalanceId}).ToArrayAsync();
 await Error("STAFF_SUPPLY_SNAPSHOT_ITEMS_MISSING",()=>f.Reserve(1));
 var after=await f.Db.SupplyReservations.AsNoTracking().Where(r=>r.WorkId==f.Work&&r.Status=="RESERVED").Select(r=>new{r.ReservationId,r.Quantity,r.BalanceId}).ToArrayAsync();
 Equal(JsonSerializer.Serialize(after),JsonSerializer.Serialize(before),"unchanged holds");
});
await Test("partial completion without linked missing report is blocked",async f=>{
 await f.Reserve(0);await f.Assess(1,1);await Error("ROOM_SUPPLIES_NOT_REPLENISHED",()=>f.Consume());Equal((await f.Balance(0)).Used,0m,"not consumed");Equal((await f.Reservation()).Quantity,3m,"hold retained");
});
await Test("partial completion with linked exact-item missing report consumes actual only",async f=>{
 await f.Reserve(0);await f.Assess(1,1);await f.Report(2);await f.Consume();await f.Consume();
 Equal((await f.Balance(0)).Used,1m,"actual used");Equal((await f.Balance(0)).Reserved,0m,"unfilled reserve released");
 Equal(await f.Db.StaffBatchBalances.Where(h=>h.BalanceId==f.BalanceIds[0]).SumAsync(h=>h.Quantity),19m,"actual batch debit");
 Equal(await f.Db.StaffSupplyLedgers.CountAsync(l=>l.BalanceId==f.BalanceIds[0]&&l.Kind=="USE"),1,"idempotent consume");
});
await Test("unlinked or insufficient missing report cannot authorize partial completion",async f=>{
 await f.Reserve(0);await f.Assess(1,1);await f.Report(9,false);await f.Report(1);await Error("ROOM_SUPPLIES_NOT_REPLENISHED",()=>f.Consume());Equal((await f.Balance(0)).Used,0m,"no debit");
});
await Test("full refill consumes only deficit and needs no missing report",async f=>{
 await f.Reserve(0);await f.Assess(3,1);await f.Consume();Equal((await f.Balance(0)).Used,1m,"actual deficit only");
});
await Test("reclassified reusable legacy quantity is removed from remaining without return or use",async f=>{
 var b=await f.AddOldOnlyItem(true);Equal(b.InitialIssued,20m,"old issue retained");Equal(b.ReclassifiedQuantity,20m,"custody transferred");Equal(SupplyMath.Remaining(b),0m,"legacy not still owned twice");Equal(b.Used,0m,"not fake use");Equal(b.Returned,0m,"not fake return");
});
var output=Path.Combine(root,"artifacts/restructure-v2/assessment-runtime-results.json");Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await using(var verify=new HotelOpsCompactDbContext(options)){
 var remaining=await verify.Users.AsNoTracking().CountAsync(u=>u.Username.StartsWith("V2AS-"));
 if(remaining!=0){failed++;results.Add(new{name="rollback cleanup verification",status="FAIL",remainingSyntheticUsers=remaining});}
 else results.Add(new{name="rollback cleanup verification",status="PASS",remainingSyntheticUsers=remaining});
}
var assemblyPath=typeof(StaffSupplyService).Assembly.Location;
var assemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(assemblyPath)));
await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new{database,at=DateTime.UtcNow,assemblySha256,mode="Real SQL Server service integration with outer transaction rollback. No HTTP server started.",limitations=new[]{"Synthetic baseline staff supply balances and batch custody seeded directly; initial stock issue/shift allocation APIs not covered.","Internal Reserve/Consume invoked via reflection; public AssessRoom and RoomAssessment run real authorization, EF queries, SQL constraints and triggers.","Every fixture transaction rolled back, no synthetic credentials or stock persisted. No real account/password/role mutations."},tests=results,passed=results.Count-failed,failed},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"RESULT {results.Count-failed} PASS / {failed} FAIL; all transactions rolled back");
Environment.ExitCode=failed==0?0:1;
static void Equal<T>(T actual,T expected,string context){if(!EqualityComparer<T>.Default.Equals(actual,expected))throw new Exception($"{context}: expected {expected}; actual {actual}");}
static async Task Error(string code,Func<Task> action){try{await action();}catch(ApiException e)when(e.Code==code){return;}throw new Exception("Expected "+code);}

sealed class Fixture{
 public required HotelOpsCompactDbContext Db; public required StaffSupplyService Service;public required IHttpContextAccessor Accessor;
 public long Branch,Warehouse,Unit,Room,Work,Item;public long[] Users=[],Sessions=[],BalanceIds=[],Allocations=[];public string Prefix="";
 public static async Task<Fixture> Create(HotelOpsCompactDbContext db){
  var f=new Fixture{Db=db,Service=null!,Accessor=new TestAccessor{HttpContext=new DefaultHttpContext()},Prefix="V2AS-"+Guid.NewGuid().ToString("N")[..12]};
  var json=await f.Scalar<string>("""
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'TEST only',1;
DECLARE @branch bigint=(SELECT TOP(1) b.branch_id FROM branches b WHERE b.is_active=1 AND EXISTS(SELECT 1 FROM departments d WHERE d.branch_id=b.branch_id AND d.code='HK') ORDER BY b.branch_id);
DECLARE @warehouse bigint=(SELECT TOP(1) warehouse_id FROM warehouses WHERE branch_id=@branch AND is_active=1 ORDER BY warehouse_id),@unit bigint=(SELECT TOP(1) reference_id FROM reference_values WHERE kind='UOM' AND is_active=1 ORDER BY reference_id),@role bigint=(SELECT role_id FROM roles WHERE code='HK_STAFF'),@department bigint=(SELECT TOP(1) department_id FROM departments WHERE branch_id=@branch AND code='HK');
INSERT users(employee_code,username,full_name,account_status,employment_status) VALUES(@p+'A',@p+'A','TEST Assessment A','ACTIVE','ACTIVE');DECLARE @a bigint=SCOPE_IDENTITY();
INSERT users(employee_code,username,full_name,account_status,employment_status) VALUES(@p+'B',@p+'B','TEST Assessment B','ACTIVE','ACTIVE');DECLARE @b bigint=SCOPE_IDENTITY();
INSERT user_roles(user_id,role_id,branch_id,assigned_at) VALUES(@a,@role,@branch,DATEADD(day,-1,SYSUTCDATETIME())),(@b,@role,@branch,DATEADD(day,-1,SYSUTCDATETIME()));
INSERT memberships(user_id,branch_id,kind,valid_from) VALUES(@a,@branch,'BRANCH_ACCESS',DATEADD(day,-1,SYSUTCDATETIME())),(@b,@branch,'BRANCH_ACCESS',DATEADD(day,-1,SYSUTCDATETIME()));
INSERT user_sessions(user_id,token_hash,security_stamp,expires_at) SELECT user_id,HASHBYTES('SHA2_256',CONVERT(varchar(40),NEWID())),security_stamp,DATEADD(day,2,SYSUTCDATETIME()) FROM users WHERE user_id IN(@a,@b);
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity,is_active) VALUES(@branch,@department,@role,@p,0,1440,2,1);DECLARE @shift bigint=SCOPE_IDENTITY();
INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,created_by) VALUES(@shift,@a,CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),'WORKING',DATEADD(minute,-5,SYSUTCDATETIME()),@a),(@shift,@b,CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),'WORKING',DATEADD(minute,-5,SYSUTCDATETIME()),@a);
INSERT staff_supply_allocations(branch_id,user_id,assignment_id,status,created_at) SELECT @branch,user_id,assignment_id,'OPEN',DATEADD(minute,-4,SYSUTCDATETIME()) FROM workday_assignments WHERE shift_id=@shift;
INSERT room_types(branch_id,code,name,capacity) VALUES(@branch,@p,'TEST Assessment room type',4);DECLARE @type bigint=SCOPE_IDENTITY();
INSERT rooms(branch_id,location_id,room_type_id,department_id,code,operational_status) SELECT TOP(1) @branch,location_id,@type,@department,@p,'DIRTY' FROM rooms WHERE branch_id=@branch AND is_active=1;DECLARE @room bigint=SCOPE_IDENTITY();
INSERT inventory_items(code,name,base_unit_id) VALUES(@p,'TEST Assessment consumable',@unit);DECLARE @item bigint=SCOPE_IDENTITY();
INSERT item_lifecycle_profiles(branch_id,item_id,classification,expiry_policy,updated_at) VALUES(@branch,@item,'CONSUMABLE','NONE',SYSUTCDATETIME());
INSERT supply_configurations(branch_id,item_id,supply_group,warehouse_id,unit_id,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,is_current,updated_by,updated_at) VALUES(@branch,@item,'A',@warehouse,@unit,20,0,1,1,1,0,@a,SYSUTCDATETIME()),(@branch,@item,'A',@warehouse,@unit,20,0,2,1,2,1,@a,SYSUTCDATETIME());
INSERT staff_supply_balances(allocation_id,configuration_id,item_id,warehouse_id,unit_id,initial_target,per_guest_quantity,initial_issued) SELECT a.allocation_id,c.configuration_id,@item,@warehouse,@unit,20,c.per_guest_quantity,20 FROM staff_supply_allocations a JOIN supply_configurations c ON c.item_id=@item AND c.revision=CASE WHEN a.user_id=@a THEN 1 ELSE 2 END WHERE a.user_id IN(@a,@b);
INSERT stock_batches(branch_id,warehouse_id,item_id,lot_number,received_at,received_quantity,warehouse_quantity,reserved_quantity,status,is_legacy,created_at) VALUES(@branch,@warehouse,@item,@p,SYSUTCDATETIME(),40,0,0,'READY',0,SYSUTCDATETIME());DECLARE @batch bigint=SCOPE_IDENTITY();
INSERT staff_batch_balances(balance_id,batch_id,quantity) SELECT balance_id,@batch,20 FROM staff_supply_balances WHERE item_id=@item;
INSERT work_items(branch_id,work_no,kind,status,title,department_id,room_id,created_by,turnover_no,actual_start_at) VALUES(@branch,@p,'CLEANING','IN_PROGRESS','TEST Assessment cleaning',@department,@room,@a,0,SYSUTCDATETIME());DECLARE @work bigint=SCOPE_IDENTITY();
INSERT work_assignments(branch_id,work_id,assignee_user_id,assigned_by,assignment_status) VALUES(@branch,@work,@a,@a,'ACTIVE');
SELECT @branch branch,@warehouse warehouse,@unit unit,@room room,@work work,@item item,@a a,@b b FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
""",("p",f.Prefix));
  var data=JsonDocument.Parse(json).RootElement;long Id(string k)=>data.GetProperty(k).GetInt64();
  f.Branch=Id("branch");f.Warehouse=Id("warehouse");f.Unit=Id("unit");f.Room=Id("room");f.Work=Id("work");f.Item=Id("item");f.Users=[Id("a"),Id("b")];
  foreach(var u in f.Users){f.Sessions=[..f.Sessions,await db.UserSessions.Where(s=>s.UserId==u).Select(s=>s.SessionId).SingleAsync()];f.Allocations=[..f.Allocations,await db.StaffSupplyAllocations.Where(a=>a.UserId==u).Select(a=>a.AllocationId).SingleAsync()];}
  foreach(var a in f.Allocations)f.BalanceIds=[..f.BalanceIds,await db.StaffSupplyBalances.Where(b=>b.AllocationId==a).Select(b=>b.BalanceId).SingleAsync()];
  var actor=new Actor(f.Accessor);var clock=TimeProvider.System;var access=new Access(db,actor,clock);f.Service=new(db,access,actor,new Atomic(db),new Audit(db,actor,clock),clock,null!,null!);f.ActAs(0);return f;
 }
 public void ActAs(int n)=>Accessor.HttpContext!.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Users[n].ToString()),new Claim("session_id",Sessions[n].ToString())],"TEST"));
 public async Task<T> Scalar<T>(string sql,params (string,object)[] args){await using var cmd=Db.Database.GetDbConnection().CreateCommand();cmd.Transaction=Db.Database.CurrentTransaction!.GetDbTransaction();cmd.CommandText=sql;foreach(var(k,v) in args){var p=cmd.CreateParameter();p.ParameterName="@"+k;p.Value=v;cmd.Parameters.Add(p);}var result=await cmd.ExecuteScalarAsync();return (T)Convert.ChangeType(result!,typeof(T));}
 public async Task Call(string method,params object[] args){var m=typeof(StaffSupplyService).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!;try{await(Task)m.Invoke(Service,args)!;}catch(TargetInvocationException e)when(e.InnerException!=null){throw e.InnerException;}}
 public Task Reserve(int n,Guid? key=null)=>Call("Reserve",Work,Users[n],key??Guid.NewGuid(),CancellationToken.None,false);
 public Task Consume()=>Call("Consume",Work,Guid.NewGuid(),CancellationToken.None);
 public Task<StaffSupplyBalance> Balance(int n)=>Db.StaffSupplyBalances.AsNoTracking().SingleAsync(b=>b.BalanceId==BalanceIds[n]);
 public Task<SupplyReservation> Reservation()=>Db.SupplyReservations.AsNoTracking().SingleAsync(r=>r.WorkId==Work&&r.Status=="RESERVED");
 public async Task Assess(decimal usable,decimal placed){var r=await Reservation();await Service.AssessRoom(Work,new(Guid.NewGuid(),[new(r.ReservationId,usable,Convert.ToBase64String(r.RowVersion),usable,0,0,placed)]),default);}
 public async Task Report(decimal quantity,bool linked=true){
  var issue=await Scalar<long>("INSERT work_items(branch_id,work_no,kind,status,title,room_id,created_by) VALUES(@b,@p,'INCIDENT','OPEN','TEST missing consumable',@r,@u); SELECT CONVERT(bigint,SCOPE_IDENTITY());",("b",Branch),("p",Prefix+Guid.NewGuid().ToString("N")[..8]),("r",Room),("u",Users[0]));
  Db.WorkEvents.Add(new(){BranchId=Branch,WorkId=issue,ActorUserId=Users[0],EventType="ROOM_ISSUE_REPORTED",OperationKey=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,SnapshotJson=JsonSerializer.Serialize(new{Kind="MISSING",ItemId=Item,Quantity=quantity})});await Db.SaveChangesAsync();
  if(linked)await Scalar<int>("INSERT work_links(branch_id,source_work_id,target_work_id,relation,created_by) VALUES(@b,@s,@t,'SOURCE',@u); SELECT 1;",("b",Branch),("s",issue),("t",Work),("u",Users[0]));
 }
 public async Task<StaffSupplyBalance> AddOldOnlyItem(bool reusable=false){
  var id=await Scalar<long>("""
INSERT inventory_items(code,name,base_unit_id) VALUES(@p,'TEST old-only item',@unit);DECLARE @item bigint=SCOPE_IDENTITY();
INSERT item_lifecycle_profiles(branch_id,item_id,classification,expiry_policy,updated_at) VALUES(@branch,@item,@class,'NONE',SYSUTCDATETIME());
INSERT supply_configurations(branch_id,item_id,supply_group,warehouse_id,unit_id,initial_quantity,minimum_stock,per_guest_quantity,is_active,revision,is_current,updated_by,updated_at) VALUES(@branch,@item,'A',@warehouse,@unit,20,0,1,1,1,1,@user,SYSUTCDATETIME());DECLARE @config bigint=SCOPE_IDENTITY();
INSERT staff_supply_balances(allocation_id,configuration_id,item_id,warehouse_id,unit_id,initial_target,per_guest_quantity,initial_issued,reclassified_quantity) VALUES(@allocation,@config,@item,@warehouse,@unit,20,1,20,@reclassified);SELECT CONVERT(bigint,SCOPE_IDENTITY());
""",("p",Prefix+"-only"),("unit",Unit),("branch",Branch),("warehouse",Warehouse),("user",Users[0]),("allocation",Allocations[0]),("class",reusable?"REUSABLE":"CONSUMABLE"),("reclassified",reusable?20:0));
  if(!reusable){Db.SupplyReservations.Add(new(){BalanceId=id,WorkId=Work,RoomId=Room,Capacity=4,PerGuestQuantity=1,Quantity=4,CreatedAt=DateTime.UtcNow});var b=await Db.StaffSupplyBalances.SingleAsync(x=>x.BalanceId==id);b.Reserved=4;await Db.SaveChangesAsync();}
  return await Db.StaffSupplyBalances.AsNoTracking().SingleAsync(b=>b.BalanceId==id);
 }
}
sealed class TestAccessor:IHttpContextAccessor { public HttpContext? HttpContext {get;set;} }
