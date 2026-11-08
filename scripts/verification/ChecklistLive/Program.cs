using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

if (args.Length != 3 || args[0] is not ("serve" or "bind" or "verify" or "probe" or "resume")) throw new ArgumentException("mode environment-receipt output-directory");
using var env = JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
var e = env.RootElement; var name = e.GetProperty("database").GetString()!; var owner = e.GetProperty("owner").GetString()!;
if (!e.GetProperty("success").GetBoolean() || !e.GetProperty("createdByThisRun").GetBoolean()
    || !System.Text.RegularExpressions.Regex.IsMatch(name, @"^HotelOps_Canonical_\d{8}_\d{6}_[a-f0-9]{8}_Fresh$")
    || e.GetProperty("server").GetString() != @"DESKTOP-JI8U50T\SQLEXPRESS") throw new InvalidOperationException("Isolation receipt rejected");
var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
var backend = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../../"));
var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { DataSource=@"lpc:.\SQLEXPRESS", InitialCatalog=name, IntegratedSecurity=true, TrustServerCertificate=true, ConnectTimeout=5, ApplicationName="HotelOps FE05 INT001 owned synthetic trial" }.ConnectionString;
await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
await db.Database.OpenConnectionAsync();
async Task Guard()
{
    await using var q = db.Database.GetDbConnection().CreateCommand();
    q.CommandText="SELECT CONVERT(nvarchar(128),@@SERVERNAME),DB_NAME(),(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.FE05INT001.Owner')";
    await using var r=await q.ExecuteReaderAsync();
    if(!await r.ReadAsync() || r.GetString(0)!=@"DESKTOP-JI8U50T\SQLEXPRESS" || r.GetString(1)!=name || r.IsDBNull(2) || r.GetString(2)!=owner) throw new InvalidOperationException("SQL identity/ownership rejected");
}
async Task Save() { await Guard(); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); }
async Task Write(string file, object value) => await File.WriteAllTextAsync(Path.Combine(output,file),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
await Guard();
if(args[0]=="probe") { Console.WriteLine("PASS owned database identity"); return; }
var branch = await db.Branches.Where(b=>b.BranchCode=="BR001").Select(b=>b.BranchId).SingleAsync();
var foreign = await db.Branches.Where(b=>b.BranchCode=="BR002").Select(b=>b.BranchId).SingleAsync();
if(args[0]=="bind")
{
    var main=await db.InspectionChecklists.AsNoTracking().SingleAsync(c=>c.ChecklistCode=="INT_MAIN");
    var version=await db.InspectionChecklistVersions.AsNoTracking().SingleAsync(v=>v.ChecklistId==main.ChecklistId && v.Status=="PUBLISHED");
    if(await db.RoomInspections.AnyAsync()) throw new InvalidOperationException("Historical fixture already exists; no reset");
    var actor=await db.Users.Where(u=>u.Username=="int.hk").Select(u=>u.UserId).SingleAsync();
    var type=await db.BranchRoomTypes.Where(t=>t.BranchId==branch).Select(t=>t.RoomTypeId).SingleAsync();
    var floor=new Floor{BranchId=branch,FloorCode="INT",FloorName="Synthetic integration",IsActive=true};
    db.Add(floor);await Save();
    var room=new Room{BranchId=branch,FloorId=floor.FloorId,RoomTypeId=type,RoomNumber="INT001",OperationalStatus="AVAILABLE",IsActive=true};
    db.Add(room);await Save();
    var inspection=new RoomInspection{InspectionNo="INT_HISTORY",RoomId=room.RoomId,VersionId=version.VersionId,InspectorUserId=actor,Status="PENDING",CreatedAt=DateTime.UtcNow};
    db.Add(inspection);await Save();
    await Write("historical.json",new{checklistId=main.ChecklistId,versionId=version.VersionId,inspectionId=inspection.InspectionId,fixtureOnly=true});
    Console.WriteLine("PASS synthetic historical version binding; persisted FK verified");return;
}
if(args[0]=="verify")
{
    var main=await db.InspectionChecklists.AsNoTracking().SingleAsync(c=>c.ChecklistCode=="INT_MAIN");
    var versions=await db.InspectionChecklistVersions.AsNoTracking().Where(v=>v.ChecklistId==main.ChecklistId).OrderBy(v=>v.VersionNo).Select(v=>new{v.VersionId,v.VersionNo,v.Status}).ToArrayAsync();
    var ids=versions.Select(v=>v.VersionId).ToArray();
    var criteria=await db.InspectionChecklistItems.AsNoTracking().Where(i=>ids.Contains(i.VersionId)).OrderBy(i=>i.VersionId).Select(i=>new{i.VersionId,i.Criterion}).ToArrayAsync();
    var history=await db.RoomInspections.AsNoTracking().Select(i=>new{i.InspectionId,i.VersionId,i.Status}).ToArrayAsync();
    var sessions=await db.UserSessions.CountAsync();
    var audit=await db.AuditLogs.CountAsync(a=>a.EntityType=="inspection_checklists");
    var deniedCsrf=await db.InspectionChecklists.AnyAsync(c=>c.ChecklistCode=="INT_NO_CSRF");
    if(versions.Length!=2 || versions.Any(v=>v.Status!="RETIRED") || history.Length!=1 || history[0].VersionId!=versions[0].VersionId
        || !criteria.Any(c=>c.VersionId==versions[0].VersionId && c.Criterion=="Edited Draft") || !criteria.Any(c=>c.VersionId==versions[1].VersionId && c.Criterion=="Next version") || deniedCsrf || sessions<2 || audit<7) throw new InvalidOperationException("Persistence verification failed");
    await Write("persistence.json",new{success=true,database=name,owner,versions,criteria,history,sessions,audit,csrfRejectedWithoutInsert=!deniedCsrf});
    Console.WriteLine("PASS SQL persistence, real sessions, audit, historical FK and immutable content");return;
}
string password; long typeId;
if(args[0]=="resume")
{
    using var prior=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"runtime.private.json")));
    password=prior.RootElement.GetProperty("password").GetString()!;
    typeId=prior.RootElement.GetProperty("typeId").GetInt64();
    if(await new StartupSchemaValidator(db).ValidateAsync(default)!=SchemaCompatibility.Compatible || await ChecklistSchema.StateAsync(db,default)!=1
        || !await db.Users.AnyAsync(u=>u.Username=="int.hk") || !await db.BranchRoomTypes.AnyAsync(t=>t.RoomTypeId==typeId && t.BranchId==branch)) throw new InvalidOperationException("Resume readiness rejected");
}
else
{
if(await db.Users.AnyAsync() || await db.RoomTypes.AnyAsync()) throw new InvalidOperationException("Synthetic fixture setup requires empty personnel/types; no reuse/reset");
if(await new StartupSchemaValidator(db).ValidateAsync(default)!=SchemaCompatibility.Compatible || await ChecklistSchema.StateAsync(db,default)!=1 || await RoomTypeSchema.ReadAsync(db,default)!=RoomTypeContract.BranchV1) throw new InvalidOperationException("Schema readiness failed");
password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"aA1-";
var hasher=new PasswordCredentialService(Options.Create(new PasswordHasherOptions{CompatibilityMode=PasswordHasherCompatibilityMode.IdentityV3,IterationCount=PasswordCredentialService.BaselineIterationCount}));
var users=new List<User>();
foreach(var login in new[]{"int.hk","int.denied"})
{
    var user=new User{EmployeeCode=login,Username=login,Email=login+"@example.invalid",FullName="Synthetic "+login,PrimaryBranchId=branch,EmploymentStatus="ACTIVE",AccountStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
    user.PasswordHash=hasher.HashPassword(user,password);users.Add(user);db.Add(user);
}
await Save();
var role=await db.Roles.SingleAsync(r=>r.RoleCode=="HK_SUPERVISOR");
var permission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="CHECKLIST_MANAGE");
var master=await db.Permissions.SingleAsync(p=>p.PermissionCode=="MASTER_DATA_MANAGE");
if(await db.RolePermissions.AnyAsync(p=>p.RoleId==role.RoleId && p.PermissionId==master.PermissionId)) throw new InvalidOperationException("Unexpected HK master-data grant");
// Matches the accepted Task72 isolated fixture: existing role/code, trial-only BRANCH grant.
// This is synthetic authorization data, NOT a production catalog amendment or implicit role bypass.
if(!await db.RolePermissions.AnyAsync(p=>p.RoleId==role.RoleId && p.PermissionId==permission.PermissionId)) db.Add(new RolePermission{RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="BRANCH",GrantedAt=DateTime.UtcNow});
db.Add(new UserRole{UserId=users[0].UserId,RoleId=role.RoleId,BranchId=branch,AssignedAt=DateTime.UtcNow.AddHours(-1),IsActive=true});
db.Add(new UserBranchAccess{UserId=users[0].UserId,BranchId=branch,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddHours(-1)});
await Save();
// Existing UC017 contract requires a published generic bootstrap checklist, never NULL.
var legacy=new InspectionChecklist{ChecklistCode="INT_LEGACY",ChecklistName="Synthetic generic bootstrap",IsActive=true};
var legacyVersion=new InspectionChecklistVersion{Checklist=legacy,VersionNo=1,Status="PUBLISHED"};
db.Add(new InspectionChecklistItem{Version=legacyVersion,Criterion="Synthetic bootstrap",IsRequired=true,IsActive=true});
await Save();await Guard();
await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_types(branch_id,type_code,type_name,standard_capacity,is_active,standard_cleaning_minutes,default_checklist_id) VALUES({branch},{"INT_TYPE"},{"Synthetic room type"},2,1,30,{legacy.ChecklistId})");
typeId=await db.BranchRoomTypes.Where(t=>t.BranchId==branch).Select(t=>t.RoomTypeId).SingleAsync();
}
using var rsa=RSA.Create(2048);var certReq=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");san.AddIpAddress(System.Net.IPAddress.Loopback);certReq.CertificateExtensions.Add(san.Build());
using var cert=certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(1));
var certPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));var pfx=Path.Combine(output,"runtime.private.pfx");
await File.WriteAllBytesAsync(pfx,cert.Export(X509ContentType.Pfx,certPassword));
await File.WriteAllTextAsync(Path.Combine(output,"runtime.private.cert.pem"),cert.ExportCertificatePem());
await File.WriteAllTextAsync(Path.Combine(output,"runtime.private.key.pem"),rsa.ExportPkcs8PrivateKeyPem());
await Write("runtime.private.json",new{branch,foreign,typeId,password,api="https://127.0.0.1:7447",origin="https://127.0.0.1:7448"});
await Write("runtime.json",new{database=name,owner,branch,foreign,typeId,httpsApi="https://127.0.0.1:7447",httpsFrontend="https://127.0.0.1:7448",certificateThumbprint=cert.Thumbprint,syntheticGrant="Existing CHECKLIST_MANAGE/BRANCH on existing HK_SUPERVISOR, isolated Task72 fixture only",masterDataGrantedToHK=false});
await Guard();
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.Combine(backend,"src/HotelOps.Api"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
start.ArgumentList.Add(Path.Combine(backend,"src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["DOTNET_ENVIRONMENT"]="Development";
start.Environment["ASPNETCORE_URLS"]="https://127.0.0.1:7447";start.Environment["ConnectionStrings__HotelOpsDb"]=connection;
start.Environment["Kestrel__Certificates__Default__Path"]=pfx;start.Environment["Kestrel__Certificates__Default__Password"]=certPassword;
start.Environment["Logging__LogLevel__Default"]="None";
using var process=Process.Start(start)??throw new InvalidOperationException("API start failed");
var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
Console.WriteLine("READY: isolated fixtures + HTTPS API process started; no credentials printed");
try
{
    var deadline=DateTime.UtcNow.AddMinutes(15);
    while(!File.Exists(Path.Combine(output,"stop")) && DateTime.UtcNow<deadline && !process.HasExited) await Task.Delay(500);
    if(process.HasExited) { await Write("startup-failure.json",new{exitCode=process.ExitCode,log=await stdout+await stderr}); throw new InvalidOperationException("API exited; inspect local sanitized startup receipt"); }
}
finally{if(!process.HasExited)process.Kill(true);await process.WaitForExitAsync();}