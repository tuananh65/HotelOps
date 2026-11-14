using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;

if(args.Length<2)throw new InvalidOperationException("mode and receipt directory required");
var mode=args[0];var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
var builder=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DEV2_TRIAL_CONNECTION")??throw new InvalidOperationException("Missing private process connection configuration"));
if(builder.InitialCatalog!=Dev2DemoPolicyIdentity.Database||!builder.IntegratedSecurity||builder.DataSource is not("lpc:KIENPC" or "lpc:KIENPC\\SQLEXPRESS" or "KIENPC" or "KIENPC\\SQLEXPRESS"))throw new InvalidOperationException("Exact local trial/Windows authentication required");
var options=new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(builder.ConnectionString).Options;
HotelOpsDbContext Open()=>new(options);
var jsonOptions=new JsonSerializerOptions{WriteIndented=true};
Task Write(string name,object data)=>File.WriteAllTextAsync(Path.Combine(output,name),JsonSerializer.Serialize(data,jsonOptions));
await using var db=Open();await db.Database.OpenConnectionAsync();
var identity=await db.Database.SqlQueryRaw<int>("SELECT CAST(CASE WHEN DB_NAME()=N'HotelOps_DEV2_FullStack_Trial' AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=N'KIENPC' THEN 1 ELSE 0 END AS int) AS [Value]").SingleAsync();
if(identity!=1)throw new InvalidOperationException("Actual database identity mismatch; no write");
var guid=await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(36),database_guid) AS [Value] FROM sys.database_recovery_status WHERE database_id=DB_ID()").SingleAsync();
if(mode=="schema")
{
 var actual=new HashSet<string>(await db.Database.SqlQueryRaw<string>("SELECT t.name+N'.'+c.name AS [Value] FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id").ToListAsync(),StringComparer.OrdinalIgnoreCase);
 var missing=new List<string>();foreach(var entity in db.Model.GetEntityTypes()){var table=StoreObjectIdentifier.Create(entity,StoreObjectType.Table);if(table is null)continue;foreach(var property in entity.GetProperties()){var name=entity.GetTableName()+"."+property.GetColumnName(table.Value);if(!actual.Contains(name))missing.Add(name);}}
 var auth=await new StartupSchemaValidator(db).ValidateAsync(CancellationToken.None);
 var passed=missing.Count==0&&auth==SchemaCompatibility.Compatible;
 await Write("EF_SCHEMA_COMPATIBILITY.json",new{status=passed?"PASS":"FAIL",database=builder.InitialCatalog,databaseGuid=guid,missingMappedColumns=missing,authenticationSchema=auth.ToString(),scope="All mapped column presence + existing Auth validator; constraints/types snapshot remains separate; no DDL/DML"});
 if(!passed)throw new InvalidOperationException("Schema incompatible; fixture writes blocked");return;
}
if(mode!="e2e"||args.Length<3)throw new InvalidOperationException("Unknown mode/API URL required");
var api=new Uri(args[2]);if(api.Scheme!="https"||!api.IsLoopback)throw new InvalidOperationException("Trusted HTTPS localhost API required; no certificate bypass");
var compatibility=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output,"EF_SCHEMA_COMPATIBILITY.json")));
if(compatibility.RootElement.GetProperty("status").GetString()!="PASS"||compatibility.RootElement.GetProperty("databaseGuid").GetString()!=guid)throw new InvalidOperationException("Schema receipt not bound to target");
var backup=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output,"BACKUP_AND_ROLLBACK.json")));
if(backup.RootElement.GetProperty("backupVerified").GetBoolean()!=true||backup.RootElement.GetProperty("databaseGuid").GetString()!=guid)throw new InvalidOperationException("Verified target-bound backup required before fixtures");
var marker="D2F"+Guid.NewGuid().ToString("N")[..10];var password="D2!"+Convert.ToHexString(RandomNumberGenerator.GetBytes(20))+"a";
var hasher=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
var actor=new User{Username=marker+".actor",EmployeeCode=marker+"A",Email=marker+"a@example.invalid",FullName=marker,EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=DateTime.UtcNow};actor.PasswordHash=hasher.HashPassword(actor,password);
var denied=new User{Username=marker+".denied",EmployeeCode=marker+"D",Email=marker+"d@example.invalid",FullName=marker,EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=DateTime.UtcNow};denied.PasswordHash=hasher.HashPassword(denied,password);
var viewer=new User{Username=marker+".viewer",EmployeeCode=marker+"V",Email=marker+"v@example.invalid",FullName=marker,EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=DateTime.UtcNow};viewer.PasswordHash=hasher.HashPassword(viewer,password);
var a=new Branch{BranchCode=marker+"A",BranchName=marker,IsActive=true,CreatedAt=DateTime.UtcNow};var b=new Branch{BranchCode=marker+"B",BranchName=marker,IsActive=true,CreatedAt=DateTime.UtcNow};
var role=new Role{RoleCode=marker+"_WRITE",RoleName=marker,IsActive=true,IsSystemRole=false,CreatedAt=DateTime.UtcNow};var viewRole=new Role{RoleCode=marker+"_VIEW",RoleName=marker,IsActive=true,IsSystemRole=false,CreatedAt=DateTime.UtcNow};
var type=new AssetType{TypeCode=marker,TypeName=marker,IsActive=true};
var codes=new[]{"ASSET_MANAGE","ASSET_TRANSFER","INCIDENT_CREATE","ROOM_VIEW"};var permissions=await db.Permissions.Where(x=>codes.Contains(x.PermissionCode)).ToDictionaryAsync(x=>x.PermissionCode);
if(permissions.Count!=4)throw new InvalidOperationException("Actual canonical catalog lacks priority permissions; no guessed replacement/seed");
Area Area(Branch branch,string suffix)=>new(){Branch=branch,AreaCode=marker+suffix,AreaName=marker,IsActive=true};
var area1=Area(a,"1");var area2=Area(a,"2");var foreign=Area(b,"B");
Asset Asset(string suffix)=>new(){AssetCode=marker+suffix,AssetName=marker,AssetType=type,Branch=a,ConditionStatus="GOOD",LifecycleStatus="IN_USE",CreatedAt=DateTime.UtcNow};
var asset=Asset("A");var race=Asset("R");var empty=Asset("E");
await using(var tx=await db.Database.BeginTransactionAsync())
{
 db.AddRange(a,b,role,viewRole,type,area1,area2,foreign,asset,race,empty);await db.SaveChangesAsync();
 foreach(var user in new[]{actor,denied,viewer}){user.PrimaryBranchId=a.BranchId;db.Add(user);}await db.SaveChangesAsync();
 foreach(var user in new[]{actor,denied,viewer})db.Add(new UserBranchAccess{UserId=user.UserId,BranchId=a.BranchId,AccessLevel="STANDARD",IsPrimary=true,GrantedAt=DateTime.UtcNow});
 foreach(var code in codes.Take(3))db.Add(new RolePermission{RoleId=role.RoleId,PermissionId=permissions[code].PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
 db.Add(new RolePermission{RoleId=viewRole.RoleId,PermissionId=permissions["ROOM_VIEW"].PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
 db.Add(new UserRole{UserId=actor.UserId,RoleId=role.RoleId,BranchId=a.BranchId,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)});
 db.Add(new UserRole{UserId=viewer.UserId,RoleId=viewRole.RoleId,BranchId=a.BranchId,IsActive=true,AssignedAt=DateTime.UtcNow.AddMinutes(-1)});await db.SaveChangesAsync();await tx.CommitAsync();
}
var userIds=new[]{actor.UserId,denied.UserId,viewer.UserId};var assetIds=new[]{asset.AssetId,race.AssetId,empty.AssetId};
await Write("FIXTURE_MANIFEST.json",new{marker,database=builder.InitialCatalog,databaseGuid=guid,users=userIds,assets=assetIds,branches=new[]{a.BranchId,b.BranchId},roles=new[]{role.RoleId,viewRole.RoleId},areas=new[]{area1.AreaId,area2.AreaId,foreign.AreaId},assetType=type.AssetTypeId,passwordPersisted=false,grants="4 role_permission rows on two fixture-only roles; canonical permissions untouched"});
var results=new List<object>();bool failure=false;var phase="start";
void Check(bool condition,string name){results.Add(new{name,pass=condition});if(!condition)throw new InvalidOperationException("Case failed: "+name);}
HttpClient Client()=>new(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=api,Timeout=TimeSpan.FromSeconds(45)};
async Task<string> Csrf(HttpClient client){using var r=await client.GetAsync("/api/auth/csrf");r.EnsureSuccessStatusCode();var body=await r.Content.ReadFromJsonAsync<JsonElement>();return body.GetProperty("csrfToken").GetString()!;}
async Task<string> Login(HttpClient client,User user){var token=await Csrf(client);using var message=new HttpRequestMessage(HttpMethod.Post,"/api/auth/login"){Content=JsonContent.Create(new{username=user.Username,password})};message.Headers.Add(HotelOpsAntiforgery.HeaderName,token);using var r=await client.SendAsync(message);Check(r.StatusCode==HttpStatusCode.OK,"real login "+user.UserId);var body=await r.Content.ReadFromJsonAsync<JsonElement>();return body.GetProperty("csrfToken").GetString()!;}
async Task<HttpResponseMessage> Post(HttpClient client,string path,object body,string? token){var message=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};if(token is not null)message.Headers.Add(HotelOpsAntiforgery.HeaderName,token);return await client.SendAsync(message);}
try
{
 using var client=Client();var token=await Login(client,actor);phase="assignment";
 using(var r=await Post(client,$"/api/assets/{asset.AssetId}/location",new{actorUserId=actor.UserId,areaId=area1.AreaId},token))Check(r.StatusCode==HttpStatusCode.OK,"016 positive HTTP");
 await using(var check=Open()){Check(await check.Assets.AsNoTracking().AnyAsync(x=>x.AssetId==asset.AssetId&&x.CurrentAreaId==area1.AreaId),"016 persisted location");Check(await check.AssetLocationHistories.AnyAsync(x=>x.AssetId==asset.AssetId&&x.MovedBy==actor.UserId),"016 server history actor");Check(await check.AuditLogs.AnyAsync(x=>x.EntityType=="assets"&&x.EntityId==asset.AssetId.ToString()&&x.ActorUserId==actor.UserId),"016 audit actor");}
 phase="transfer";
 var transfer=new{actorUserId=actor.UserId,areaId=area2.AreaId,expectedLocationType="AREA",expectedLocationId=area1.AreaId,reason="Owner isolated trial transfer"};
 using(var r=await Post(client,$"/api/assets/{asset.AssetId}/location-transfers",transfer,token))Check(r.StatusCode==HttpStatusCode.OK,"017 positive HTTP");
 using(var r=await Post(client,$"/api/assets/{asset.AssetId}/location-transfers",transfer,token))Check(r.StatusCode==HttpStatusCode.BadRequest,"017 stale location rejected");
 using(var r=await Post(client,$"/api/assets/{empty.AssetId}/location",new{actorUserId=denied.UserId,areaId=area1.AreaId},token))Check(r.StatusCode==HttpStatusCode.Forbidden,"016 spoofed actor denied");
 using(var r=await Post(client,$"/api/assets/{empty.AssetId}/location",new{actorUserId=actor.UserId,areaId=foreign.AreaId},token))Check(r.StatusCode==HttpStatusCode.Forbidden,"016 cross-branch target denied");
 using(var r=await Post(client,$"/api/assets/{empty.AssetId}/location",new{actorUserId=actor.UserId,areaId=area1.AreaId},null))Check(r.StatusCode==HttpStatusCode.BadRequest,"016 missing CSRF denied");
 await using(var check=Open())Check(await check.Assets.AsNoTracking().AnyAsync(x=>x.AssetId==empty.AssetId&&x.CurrentAreaId==null),"denied assignment no DB effect");
 using var noRights=Client();var noToken=await Login(noRights,denied);
 using(var r=await Post(noRights,$"/api/assets/{empty.AssetId}/location",new{actorUserId=denied.UserId,areaId=area1.AreaId},noToken))Check(r.StatusCode==HttpStatusCode.Forbidden,"016 missing grant denied");
 phase="incident";var incidentBody=new{branchId=a.BranchId,reportedBy=actor.UserId,areaId=area1.AreaId,incidentTitle=marker,description=marker,discoveredAt=DateTime.UtcNow};
 using(var r=await Post(client,"/api/incidents",incidentBody,token))Check(r.StatusCode==HttpStatusCode.Created,"021 positive HTTP");
 await using(var check=Open()){var created=await check.Incidents.AsNoTracking().SingleAsync(x=>x.IncidentTitle==marker);Check(created.ReportedBy==actor.UserId,"021 server reporter");Check(await check.AuditLogs.AnyAsync(x=>x.EntityType=="incidents"&&x.EntityId==created.IncidentId.ToString()&&x.ActorUserId==actor.UserId),"021 audit actor");}
 using var view=Client();var viewToken=await Login(view,viewer);
 using(var r=await Post(view,"/api/incidents",new{branchId=a.BranchId,reportedBy=viewer.UserId,areaId=area1.AreaId,incidentTitle=marker+"deny",description=marker,discoveredAt=DateTime.UtcNow},viewToken))Check(r.StatusCode==HttpStatusCode.Forbidden,"021 ROOM_VIEW cannot authorize POST");
 phase="revocation";await using(var change=Open()){var grant=await change.UserRoles.SingleAsync(x=>x.UserId==actor.UserId&&x.RoleId==role.RoleId);grant.IsActive=false;await change.SaveChangesAsync();}
 using(var r=await Post(client,$"/api/assets/{empty.AssetId}/location",new{actorUserId=actor.UserId,areaId=area1.AreaId},token))Check(r.StatusCode==HttpStatusCode.Forbidden,"016 fresh revocation denied");
 await using(var change=Open()){var grant=await change.UserRoles.SingleAsync(x=>x.UserId==actor.UserId&&x.RoleId==role.RoleId);grant.IsActive=true;await change.SaveChangesAsync();}
 phase="concurrency";using var parallel=Client();var parallelToken=await Login(parallel,actor);
 var responses=await Task.WhenAll(Post(client,$"/api/assets/{race.AssetId}/location",new{actorUserId=actor.UserId,areaId=area1.AreaId},token),Post(parallel,$"/api/assets/{race.AssetId}/location",new{actorUserId=actor.UserId,areaId=area2.AreaId},parallelToken));
 Check(responses.Count(x=>x.StatusCode==HttpStatusCode.OK)==1&&responses.Count(x=>x.StatusCode==HttpStatusCode.BadRequest)==1,"016 exactly-one-success concurrent assignment");foreach(var r in responses)r.Dispose();
 await using(var check=Open())Check(await check.AssetLocationHistories.CountAsync(x=>x.AssetId==race.AssetId)==1,"016 one committed location history");
 phase="rollback";await using(var change=Open()){await using(var tx=await change.Database.BeginTransactionAsync()){change.AssetLocationHistories.Add(new AssetLocationHistory{AssetId=empty.AssetId,ToLocationType="AREA",ToLocationId=area1.AreaId,EffectiveAt=DateTime.UtcNow,MovedBy=actor.UserId,Reason=marker});await change.SaveChangesAsync();await tx.RollbackAsync();}}
 await using(var check=Open())Check(!await check.AssetLocationHistories.AnyAsync(x=>x.AssetId==empty.AssetId),"SQL rollback primitive removed staged history (not audit-fault E2E)");
 using(var r=await Post(client,"/api/auth/logout",new{},token))Check(r.StatusCode==HttpStatusCode.NoContent,"real logout");
 using(var r=await client.GetAsync("/api/auth/session"))Check(r.StatusCode==HttpStatusCode.Unauthorized,"logout session invalidated");
}
catch(Exception){failure=true;await Write("SQL_E2E_FAILURE.json",new{phase,marker,status="FAIL",details="Inspect status-only case receipts and sanitized API logs; secrets/body/credential data intentionally omitted"});}
finally
{
 password=string.Empty;
 try
 {
  await using var clean=Open();await using var cleanup=await clean.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
  var sameGuid=await clean.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(36),database_guid) AS [Value] FROM sys.database_recovery_status WHERE database_id=DB_ID() AND DB_NAME()=N'HotelOps_DEV2_FullStack_Trial' AND CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=N'KIENPC'").SingleAsync();
  if(sameGuid!=guid)throw new InvalidOperationException("Cleanup target changed");
  if(await clean.Users.CountAsync(x=>userIds.Contains(x.UserId)&&x.FullName==marker)!=3)throw new InvalidOperationException("Fixture identity mismatch");
  var incidents=await clean.Incidents.Where(x=>userIds.Contains(x.ReportedBy)&&x.IncidentTitle==marker).Select(x=>x.IncidentId).ToListAsync();
  // Delete only the unique fixture graph. Unexpected external FK references cause rollback; constraints remain enabled.
  await clean.IncidentEvents.Where(x=>incidents.Contains(x.IncidentId)).ExecuteDeleteAsync();
  await clean.Incidents.Where(x=>incidents.Contains(x.IncidentId)).ExecuteDeleteAsync();
  await clean.AssetLocationHistories.Where(x=>assetIds.Contains(x.AssetId)).ExecuteDeleteAsync();
  var auditRows=await clean.AuditLogs.Where(x=>x.ActorUserId!=null&&userIds.Contains(x.ActorUserId.Value)).ExecuteDeleteAsync();
  await clean.Assets.Where(x=>assetIds.Contains(x.AssetId)).ExecuteDeleteAsync();
  await clean.Areas.Where(x=>x.AreaId==area1.AreaId||x.AreaId==area2.AreaId||x.AreaId==foreign.AreaId).ExecuteDeleteAsync();
  await clean.AssetTypes.Where(x=>x.AssetTypeId==type.AssetTypeId).ExecuteDeleteAsync();
  await clean.UserSessions.Where(x=>userIds.Contains(x.UserId)).ExecuteDeleteAsync();
  await clean.UserRoles.Where(x=>userIds.Contains(x.UserId)).ExecuteDeleteAsync();
  await clean.UserBranchAccesses.Where(x=>userIds.Contains(x.UserId)).ExecuteDeleteAsync();
  await clean.RolePermissions.Where(x=>x.RoleId==role.RoleId||x.RoleId==viewRole.RoleId).ExecuteDeleteAsync();
  await clean.Users.Where(x=>userIds.Contains(x.UserId)).ExecuteDeleteAsync();
  await clean.Roles.Where(x=>x.RoleId==role.RoleId||x.RoleId==viewRole.RoleId).ExecuteDeleteAsync();
  await clean.Branches.Where(x=>x.BranchId==a.BranchId||x.BranchId==b.BranchId).ExecuteDeleteAsync();
  await cleanup.CommitAsync();
  var remaining=await clean.Users.CountAsync(x=>userIds.Contains(x.UserId))+await clean.Assets.CountAsync(x=>assetIds.Contains(x.AssetId))+await clean.Branches.CountAsync(x=>x.BranchId==a.BranchId||x.BranchId==b.BranchId);
  if(remaining!=0)throw new InvalidOperationException("Fixture cleanup verification failed");
  await Write("FIXTURE_CLEANUP.json",new{status="PASS",marker,databaseGuid=guid,remainingRoots=remaining,fixtureAuditRowsRemoved=auditRows,scope="Only recorded fixture graph; receipt preserves case evidence; canonical permission catalog untouched; no identity reseed or whole-database restore"});
 }
 catch(Exception)
 {
  failure=true;await Write("FIXTURE_CLEANUP.json",new{status="FAIL_REQUIRES_LOCAL_REVIEW",marker,databaseGuid=guid,details="Cleanup transaction rolls back on unexpected references; consult fixture IDs and backup. Never disable constraints or delete unrelated rows."});
 }
 await Write("SQL_TEST_RESULTS.json",new{status=failure?"FAIL":"PASS_SELECTED_CASES_ONLY",database=builder.InitialCatalog,databaseGuid=guid,results,fullyAcceptedActionIds=Array.Empty<string>(),notRun="Remaining58; complete per-action negative/expired/restricted/parent/SoD matrix and injected domain audit-fault rollback NOT_RUN. Selected passes do not satisfy FULL004."});
}
if(failure)Environment.ExitCode=1;
