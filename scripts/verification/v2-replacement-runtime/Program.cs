using System.Data;
using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

const string database="HotelOpsCompactWorkdayTestV22611250010";
var settings=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft/UserSecrets/hotelops-compact-v2-b4e6ee02-9edb-417c-a06e-358ae6367c5c/secrets.json")));
var connection=new SqlConnectionStringBuilder(settings.RootElement.GetProperty("ConnectionStrings:HotelOpsDb").GetString()){InitialCatalog=database};
var options=new DbContextOptionsBuilder<HotelOpsCompactDbContext>().UseSqlServer(connection.ConnectionString).Options;
var results=new List<object>();var failed=0;
async Task Test(string name,Func<Fixture,Task> test){
 await using var db=new HotelOpsCompactDbContext(options);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
 try{var f=await Fixture.Create(db);await test(f);results.Add(new{name,status="PASS"});Console.WriteLine("PASS "+name);}
 catch(Exception e){failed++;results.Add(new{name,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e);}
 finally{await tx.RollbackAsync();}
}
await Test("shared context retains each replacement snapshot and permits another replacement round",async f=>{
 await f.Do(0,"propose",clear:false);await f.Do(1,"confirm",clear:false);await f.Do(2,"approve",clear:false);await f.MoveOriginal();await f.Do(0,"install",f.NewAsset,clear:false);
 if(await f.Db.WorkEvents.CountAsync(e=>e.WorkId==f.Work&&e.EventType.StartsWith("REPLACEMENT_")&&e.SnapshotJson!=null)!=4)throw new Exception("Snapshot lost in shared context");
 if(await f.Db.AssetEvents.CountAsync(e=>e.SourceWorkId==f.Work&&e.EventType.StartsWith("REPLACEMENT_"))!=2)throw new Exception("Both assets need provenance");
 await f.Do(0,"propose",clear:false);if(await f.State()!="REPLACEMENT_PROPOSED")throw new Exception("Second round blocked");
});
await Test("assigned maintenance staff proposes; supervisor confirms; OM approves; real asset installed",async f=>{
 await f.Do(0,"propose");await f.Do(1,"confirm");await f.Do(2,"approve");await f.MoveOriginal();await f.Do(0,"install",asset:f.NewAsset);
 Eq(await f.State(),"REPLACEMENT_INSTALLED","state");Eq((await f.Db.WorkItems.AsNoTracking().SingleAsync(w=>w.WorkId==f.Work)).AssetId,f.NewAsset,"maintenance asset identity");
 Eq(await f.Db.WorkEvents.CountAsync(e=>e.WorkId==f.Work&&e.EventType.StartsWith("REPLACEMENT_")),4,"history");
 Eq(await f.Db.AuditLogs.CountAsync(a=>a.EntityType=="WorkItem"&&a.EntityKey==f.Work.ToString()&&a.Action.StartsWith("REPLACEMENT_")),4,"audit");
 Eq(await f.Db.Assets.CountAsync(a=>a.AssetCode.StartsWith(f.Prefix)),2,"no new asset fabricated");
 Eq(await f.Db.WorkChecklistItems.AnyAsync(i=>i.WorkId==f.Work&&i.Outcome!=null),false,"new asset requires checklist again");
});
await Test("staff cannot confirm or approve own proposal",async f=>{await f.Do(0,"propose");await Denied(()=>f.Do(0,"confirm"));await f.Do(1,"confirm");await Denied(()=>f.Do(0,"approve"));});
await Test("wrong-branch OM cannot read or approve replacement",async f=>{await f.Do(0,"propose");await f.Do(1,"confirm");f.ActAs(3);await Denied(async()=>{await f.Service.ReplacementDetail(f.Work,default);});await Denied(()=>f.Do(3,"approve"));});
await Test("unassigned staff cannot propose",async f=>{await Denied(()=>f.Do(4,"propose"));Eq(await f.State(),"NONE","no event");});
await Test("pending proposal blocks completion through public WorkService.Act",async f=>{await f.Do(0,"propose");f.ActAs(0);await Code("REPLACEMENT_NOT_INSTALLED",async()=>{await f.Service.Act(f.Work,"complete",new ActionRequest(await f.Version(),Guid.NewGuid(),"Fixture completion"),default);});});
await Test("OM rejection records reason and permits new proposal",async f=>{await f.Do(0,"propose");await f.Do(1,"confirm");await f.Do(2,"reject",reason:"Fixture reject cost");Eq(await f.State(),"REPLACEMENT_REJECTED","rejected");await f.Do(0,"propose");Eq(await f.State(),"REPLACEMENT_PROPOSED","reproposal");});
await Test("supervisor rejection records reason",async f=>{await f.Do(0,"propose");await f.Do(1,"reject",reason:"Fixture repair feasible");Eq(await f.State(),"REPLACEMENT_REJECTED","rejected");Eq(await f.Db.WorkEvents.Where(e=>e.WorkId==f.Work&&e.EventType=="REPLACEMENT_REJECTED").Select(e=>e.Reason).SingleAsync(),"Fixture repair feasible","reason");});
await Test("empty rejection reason and stale version are rejected without new events",async f=>{var initial=await f.Version();await f.Do(0,"propose");await Code("STALE_ROW_VERSION",()=>f.Do(1,"confirm",version:initial));await Code("REPLACEMENT_REASON_REQUIRED",()=>f.Do(1,"reject",reason:" "));Eq(await f.Db.WorkEvents.CountAsync(e=>e.WorkId==f.Work),1,"no new events");});
await Test("install requires approval and original asset location reconciliation",async f=>{await f.Do(0,"propose");await Code("REPLACEMENT_WRONG_STAGE",()=>f.Do(0,"install",asset:f.NewAsset));await f.Do(1,"confirm");await f.Do(2,"approve");await Code("ORIGINAL_ASSET_LOCATION_RECONCILIATION_REQUIRED",()=>f.Do(0,"install",asset:f.NewAsset));});
await Test("installed asset must be active and physically in the room",async f=>{await f.Do(0,"propose");await f.Do(1,"confirm");await f.Do(2,"approve");await f.MoveOriginal();await f.Sql<int>("UPDATE assets SET room_id=NULL,location_id=(SELECT TOP(1) location_id FROM locations WHERE branch_id=@b) WHERE asset_id=@a;SELECT 1;",("b",f.Branch),("a",f.NewAsset));await Code("REPLACEMENT_ASSET_NOT_INSTALLED",()=>f.Do(0,"install",asset:f.NewAsset));});
await Test("same proposal actor holding supervisor role cannot self-confirm",async f=>{await f.Do(0,"propose");await f.AddRole(0,"MAINTENANCE_SUPERVISOR",true);await Code("INDEPENDENT_VERIFIER_REQUIRED",()=>f.Do(0,"confirm"));});
await Test("proposal actor holding OM role cannot approve or see a decision action",async f=>{await f.Do(0,"propose");await f.Do(1,"confirm");await f.AddRole(0,"OPERATION_MANAGER",false);f.ActAs(0);var detail=JsonSerializer.SerializeToElement(await f.Service.ReplacementDetail(f.Work,default));Eq(detail.GetProperty("canDecide").GetBoolean(),false,"no self-decision action");await Code("INDEPENDENT_APPROVER_REQUIRED",()=>f.Do(0,"approve"));});
await Test("terminal work exposes neither supervisor confirmation nor OM decision",async f=>{
 await f.Do(0,"propose");await f.Sql<int>("UPDATE work_items SET status='CANCELLED' WHERE work_id=@w;SELECT 1;",("w",f.Work));f.Db.ChangeTracker.Clear();f.ActAs(1);
 var detail=JsonSerializer.SerializeToElement(await f.Service.ReplacementDetail(f.Work,default));Eq(detail.GetProperty("canConfirm").GetBoolean(),false,"terminal confirm hidden");
 await f.Sql<int>("UPDATE work_items SET status='IN_PROGRESS' WHERE work_id=@w;SELECT 1;",("w",f.Work));f.Db.ChangeTracker.Clear();await f.Do(1,"confirm");
 await f.Sql<int>("UPDATE work_items SET status='CANCELLED' WHERE work_id=@w;SELECT 1;",("w",f.Work));f.Db.ChangeTracker.Clear();f.ActAs(2);
 detail=JsonSerializer.SerializeToElement(await f.Service.ReplacementDetail(f.Work,default));Eq(detail.GetProperty("canDecide").GetBoolean(),false,"terminal decision hidden");
});
await Test("OM replacement queue is scoped and includes confirmed requests",async f=>{await f.Do(0,"propose");await f.Do(1,"confirm");f.ActAs(2);var rows=JsonSerializer.SerializeToElement(await f.Service.ReplacementQueue(f.Branch,default));Eq(rows.EnumerateArray().Any(r=>r.GetProperty("WorkId").GetInt64()==f.Work),true,"queue includes fixture");f.ActAs(3);await Denied(async()=>{await f.Service.ReplacementQueue(f.Branch,default);});});
await using(var db=new HotelOpsCompactDbContext(options)){var count=await db.Users.CountAsync(u=>u.Username.StartsWith("V2RP-"));results.Add(new{name="rollback cleanup",status=count==0?"PASS":"FAIL",remaining=count});if(count!=0)failed++;}
var path="artifacts/restructure-v2/replacement-runtime-results.json";Directory.CreateDirectory(Path.GetDirectoryName(path)!);
await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new{database,at=DateTime.UtcNow,mode="Real SQL Server service integration, not HTTP or UI; synthetic fixtures rolled back",assemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(typeof(WorkService).Assembly.Location))),passed=results.Count-failed,failed,tests=results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"RESULT {results.Count-failed} PASS / {failed} FAIL; transactions rolled back");Environment.ExitCode=failed==0?0:1;
static void Eq<T>(T actual,T expected,string message){if(!EqualityComparer<T>.Default.Equals(actual,expected))throw new Exception($"{message}: {actual} != {expected}");}
static async Task Code(string code,Func<Task> action){try{await action();}catch(ApiException e)when(e.Code==code){return;}throw new Exception("Expected "+code);}
static async Task Denied(Func<Task> action){try{await action();}catch(ApiException e)when(e.Status==403){return;}throw new Exception("Expected 403");}

sealed class Fixture{
 public required HotelOpsCompactDbContext Db;public required WorkService Service;public required IHttpContextAccessor Accessor;
 public long Branch,OtherBranch,Room,Work,Original,NewAsset,Department;public long[] Users=[],Sessions=[];public string Prefix="";
 public static async Task<Fixture> Create(HotelOpsCompactDbContext db){
  var f=new Fixture{Db=db,Service=null!,Accessor=new TestAccessor{HttpContext=new DefaultHttpContext()},Prefix="V2RP-"+Guid.NewGuid().ToString("N")[..12]};
  var raw=await f.Sql<string>("""
IF DB_NAME()<>'HotelOpsCompactWorkdayTestV22611250010' THROW 51999,'TEST only',1;
DECLARE @branch bigint=(SELECT MIN(branch_id) FROM branches WHERE is_active=1),@other bigint=(SELECT MAX(branch_id) FROM branches WHERE is_active=1);
DECLARE @department bigint=(SELECT TOP(1) department_id FROM departments WHERE branch_id=@branch AND code='MAINT');
IF @department IS NULL SET @department=(SELECT TOP(1) department_id FROM departments WHERE branch_id=@branch AND is_active=1);
DECLARE @ids TABLE(n int,id bigint,b bigint,role_code varchar(50));DECLARE @n int=0;
WHILE @n<5 BEGIN
 INSERT users(employee_code,username,full_name,account_status,employment_status) VALUES(@p+CAST(@n AS varchar(1)),@p+CAST(@n AS varchar(1)),'TEST replacement role','ACTIVE','ACTIVE');
 INSERT @ids VALUES(@n,SCOPE_IDENTITY(),CASE WHEN @n=3 THEN @other ELSE @branch END,CASE WHEN @n IN(0,4) THEN 'MAINTENANCE_STAFF' WHEN @n=1 THEN 'MAINTENANCE_SUPERVISOR' ELSE 'OPERATION_MANAGER' END);SET @n+=1;
END;
INSERT user_roles(user_id,role_id,branch_id,assigned_at) SELECT i.id,r.role_id,i.b,DATEADD(day,-1,SYSUTCDATETIME()) FROM @ids i JOIN roles r ON r.code=i.role_code;
INSERT memberships(user_id,branch_id,kind,valid_from) SELECT id,b,'BRANCH_ACCESS',DATEADD(day,-1,SYSUTCDATETIME()) FROM @ids;
INSERT memberships(user_id,branch_id,department_id,kind,valid_from) SELECT id,@branch,@department,'EMPLOYMENT',DATEADD(day,-1,SYSUTCDATETIME()) FROM @ids WHERE n<>3;
INSERT user_sessions(user_id,token_hash,security_stamp,expires_at) SELECT user_id,HASHBYTES('SHA2_256',CONVERT(varchar(40),NEWID())),security_stamp,DATEADD(day,2,SYSUTCDATETIME()) FROM users WHERE username LIKE @p+'%';
DECLARE @super bigint=(SELECT id FROM @ids WHERE n=1),@staff bigint=(SELECT id FROM @ids WHERE n=0),@role bigint=(SELECT role_id FROM roles WHERE code='MAINTENANCE_SUPERVISOR');
INSERT workday_shifts(branch_id,department_id,role_id,name,start_minute,end_minute,capacity,is_active) VALUES(@branch,@department,@role,@p,0,1440,5,1);DECLARE @shift bigint=SCOPE_IDENTITY();
INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,created_by) VALUES(@shift,@super,CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),'WORKING',DATEADD(minute,-5,SYSUTCDATETIME()),@super);
INSERT room_types(branch_id,code,name,capacity) VALUES(@branch,@p,'TEST replacement',2);DECLARE @type bigint=SCOPE_IDENTITY();
INSERT rooms(branch_id,location_id,room_type_id,department_id,code,operational_status) SELECT TOP(1) @branch,location_id,@type,@department,@p,'DIRTY' FROM rooms WHERE branch_id=@branch AND is_active=1;DECLARE @room bigint=SCOPE_IDENTITY();
INSERT assets(branch_id,asset_code,name,room_id,condition_code) VALUES(@branch,@p+'old','TEST original fixture',@room,'FAULTY');DECLARE @old bigint=SCOPE_IDENTITY();
INSERT assets(branch_id,asset_code,name,room_id,condition_code) VALUES(@branch,@p+'new','TEST real replacement fixture',@room,'GOOD');DECLARE @new bigint=SCOPE_IDENTITY();
INSERT work_items(branch_id,work_no,kind,status,title,department_id,room_id,asset_id,created_by,actual_start_at) VALUES(@branch,@p,'MAINTENANCE','IN_PROGRESS','TEST replacement workflow',@department,@room,@old,@super,SYSUTCDATETIME());DECLARE @work bigint=SCOPE_IDENTITY();
INSERT work_assignments(branch_id,work_id,assignee_user_id,assigned_by,assignment_status,accepted_at) VALUES(@branch,@work,@staff,@super,'ACTIVE',SYSUTCDATETIME());
INSERT checklist_templates(branch_id,code,version_no,name,kind,created_by) VALUES(@branch,@p,1,'TEST replacement checklist','MAINTENANCE',@super);DECLARE @checklist bigint=SCOPE_IDENTITY();
INSERT checklist_items(checklist_template_id,line_no,description) VALUES(@checklist,1,'TEST verify installed asset');DECLARE @checkitem bigint=SCOPE_IDENTITY();
UPDATE checklist_templates SET published_at=SYSUTCDATETIME() WHERE checklist_template_id=@checklist;
UPDATE work_items SET checklist_template_id=@checklist WHERE work_id=@work;
INSERT work_checklist_items(branch_id,work_id,checklist_template_id,checklist_item_id,description_snapshot,required_snapshot,weight_snapshot,outcome,checked_by,checked_at) VALUES(@branch,@work,@checklist,@checkitem,'TEST verify installed asset',1,1,'PASS',@staff,SYSUTCDATETIME());
SELECT @branch branch,@other otherBranch,@department department,@room room,@work work,@old original,@new replacement,(SELECT id FROM @ids WHERE n=0) u0,(SELECT id FROM @ids WHERE n=1) u1,(SELECT id FROM @ids WHERE n=2) u2,(SELECT id FROM @ids WHERE n=3) u3,(SELECT id FROM @ids WHERE n=4) u4 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;
""",("p",f.Prefix));
  var d=JsonDocument.Parse(raw).RootElement;long Id(string k)=>d.GetProperty(k).GetInt64();f.Branch=Id("branch");f.OtherBranch=Id("otherBranch");f.Department=Id("department");f.Room=Id("room");f.Work=Id("work");f.Original=Id("original");f.NewAsset=Id("replacement");f.Users=Enumerable.Range(0,5).Select(n=>Id("u"+n)).ToArray();
  foreach(var u in f.Users)f.Sessions=[..f.Sessions,await db.UserSessions.Where(s=>s.UserId==u).Select(s=>s.SessionId).SingleAsync()];
  var actor=new Actor(f.Accessor);var clock=TimeProvider.System;var access=new Access(db,actor,clock);var atomic=new Atomic(db);var audit=new Audit(db,actor,clock);var notifications=new NotificationService(db,actor,access,atomic,clock);var requests=new RequestWorkflowService(db,access,actor,atomic,audit,clock,null!,null!,notifications);f.Service=new WorkService(db,access,actor,atomic,audit,clock,notifications,null!,null!,null!,requests);f.ActAs(0);return f;
 }
 public void ActAs(int n)=>Accessor.HttpContext!.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Users[n].ToString()),new Claim("session_id",Sessions[n].ToString())],"TEST"));
 public async Task<T> Sql<T>(string sql,params (string,object)[] args){await using var cmd=Db.Database.GetDbConnection().CreateCommand();cmd.Transaction=Db.Database.CurrentTransaction!.GetDbTransaction();cmd.CommandText=sql;foreach(var(k,v)in args){var p=cmd.CreateParameter();p.ParameterName="@"+k;p.Value=v;cmd.Parameters.Add(p);}return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync())!,typeof(T));}
 public async Task<string> Version()=>Convert.ToBase64String(await Db.WorkItems.AsNoTracking().Where(w=>w.WorkId==Work).Select(w=>w.RowVersion).SingleAsync());
 public async Task<string> State()=>await Db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==Work&&e.EventType.StartsWith("REPLACEMENT_")).OrderByDescending(e=>e.WorkEventId).Select(e=>e.EventType).FirstOrDefaultAsync()??"NONE";
 public async Task Do(int n,string command,long? asset=null,string? version=null,string reason="Fixture reason",bool clear=true){if(clear)Db.ChangeTracker.Clear();ActAs(n);await Service.ReplaceAsset(Work,command,new ReplacementRequest(version??await Version(),Guid.NewGuid(),reason,"Fixture replace with equivalent asset",asset),default);}
 public async Task MoveOriginal()=>await Sql<int>("UPDATE assets SET room_id=NULL,location_id=(SELECT TOP(1) location_id FROM locations WHERE branch_id=@b) WHERE asset_id=@a;SELECT 1;",("b",Branch),("a",Original));
 public async Task AddRole(int n,string role,bool shift){await Sql<int>("INSERT user_roles(user_id,role_id,branch_id,assigned_at) SELECT @u,role_id,@b,DATEADD(day,-1,SYSUTCDATETIME()) FROM roles WHERE code=@r;SELECT 1;",("u",Users[n]),("b",Branch),("r",role));if(shift)await Sql<int>("INSERT workday_assignments(shift_id,user_id,work_date,status,started_at,created_by) SELECT shift_id,@u,CONVERT(date,DATEADD(hour,7,SYSUTCDATETIME())),'WORKING',DATEADD(minute,-5,SYSUTCDATETIME()),@u FROM workday_shifts WHERE name=@p;SELECT 1;",("u",Users[n]),("p",Prefix));}
}
sealed class TestAccessor:IHttpContextAccessor{public HttpContext? HttpContext{get;set;}}
