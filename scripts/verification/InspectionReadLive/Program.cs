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
var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { DataSource=@"lpc:.\SQLEXPRESS", InitialCatalog=name, IntegratedSecurity=true, TrustServerCertificate=true, ConnectTimeout=5, ApplicationName="HotelOps FE05 FAST002 owned synthetic trial" }.ConnectionString;
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
if(args[0]=="verify"){
 await Guard();
 var inspections=await db.RoomInspections.CountAsync();var tasks=await db.CleaningTasks.CountAsync();
 var results=await db.InspectionResults.AsNoTracking().Select(x=>new{x.InspectionId,x.Outcome}).ToArrayAsync();
 var defects=await db.InspectionDefects.AsNoTracking().Select(x=>new{x.Description,x.SeverityId}).ToArrayAsync();
 var audit=await db.AuditLogs.CountAsync(x=>x.EntityType=="room_inspections");
 if(inspections!=2||tasks!=3||results.Length!=1||results[0].Outcome!="FAIL"||defects.Length!=1||audit!=3)throw new InvalidOperationException("Read/selector persistence verification failed");
 await Write("persistence.json",new{success=true,database=name,owner,inspections,tasks,results,defects,audit,assignmentWriterInvoked=false});
 Console.WriteLine("PASS owned SQL persistence; no assignment writer invoked");return;
}string password; long typeId;
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
foreach(var code in new[]{"INSPECTION_PERFORM","INSPECTION_VIEW"})
 if(!await db.RolePermissions.AnyAsync(p=>p.RoleId==role.RoleId && p.Permission.PermissionCode==code))throw new InvalidOperationException("Missing existing role grant: "+code);db.Add(new UserRole{UserId=users[0].UserId,RoleId=role.RoleId,BranchId=branch,AssignedAt=DateTime.UtcNow.AddHours(-1),IsActive=true});
db.Add(new UserBranchAccess{UserId=users[0].UserId,BranchId=branch,AccessLevel="STANDARD",GrantedAt=DateTime.UtcNow.AddHours(-1)});
await Save();
// Existing UC017 contract requires a published generic bootstrap checklist, never NULL.
var legacy=new InspectionChecklist{ChecklistCode="INT_LEGACY",ChecklistName="Synthetic generic bootstrap",IsActive=true};
var legacyVersion=new InspectionChecklistVersion{Checklist=legacy,VersionNo=1,Status="PUBLISHED"};
db.Add(new InspectionChecklistItem{Version=legacyVersion,Criterion="Synthetic bootstrap",IsRequired=true,IsActive=true});
await Save();await Guard();
await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_types(branch_id,type_code,type_name,standard_capacity,is_active,standard_cleaning_minutes,default_checklist_id) VALUES({branch},{"INT_TYPE"},{"Synthetic room type"},2,1,30,{legacy.ChecklistId})");
typeId=await db.BranchRoomTypes.Where(t=>t.BranchId==branch).Select(t=>t.RoomTypeId).SingleAsync();
var floor=new Floor{BranchId=branch,FloorCode="FAST",FloorName="Synthetic execution",IsActive=true};db.Add(floor);await Save();
var cleaningType=await db.ReferenceValues.Where(x=>x.GroupCode=="CLEANING_TYPE"&&x.IsActive).Select(x=>x.ReferenceId).FirstAsync();
foreach(var n in new[]{1,2,3}){
 var room=new Room{BranchId=branch,FloorId=floor.FloorId,RoomTypeId=typeId,RoomNumber="FAST"+n,OperationalStatus="AVAILABLE",IsActive=true};db.Add(room);await Save();
 var task=new CleaningTask{TaskNo="FAST_TASK_"+n,RoomId=room.RoomId,CleaningTypeId=cleaningType,Status="IN_PROGRESS",CreatedBy=users[0].UserId,CreatedAt=DateTime.UtcNow};db.Add(task);await Save();
 await Guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_tasks SET workflow_status={"PENDING_INSPECTION"} WHERE cleaning_task_id={task.CleaningTaskId}");
 if(n==3)continue;
 var insp=new RoomInspection{InspectionNo="FAST_INSP_"+n,RoomId=room.RoomId,CleaningTaskId=task.CleaningTaskId,VersionId=legacyVersion.VersionId,InspectorUserId=users[0].UserId,Status="PENDING",CreatedAt=DateTime.UtcNow};db.Add(insp);await Save();
 await Guard();await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_inspections SET workflow_status={"ASSIGNED"} WHERE inspection_id={insp.InspectionId}");
}
}
await Guard();await db.ReferenceValues.Where(x=>x.GroupCode=="SEVERITY"&&x.ValueCode=="MAJOR").ExecuteUpdateAsync(x=>x.SetProperty(v=>v.IsActive,false));
var majorId=await db.ReferenceValues.Where(x=>x.GroupCode=="SEVERITY"&&x.ValueCode=="MAJOR").Select(x=>x.ReferenceId).SingleAsync();
var taskId=await db.CleaningTasks.Where(x=>x.TaskNo=="FAST_TASK_3").Select(x=>x.CleaningTaskId).SingleAsync();
var inspectionIds=await db.RoomInspections.OrderBy(x=>x.InspectionId).Select(x=>x.InspectionId).ToArrayAsync();
var severityId=await db.ReferenceValues.Where(x=>x.GroupCode=="SEVERITY"&&x.ValueCode=="MINOR"&&x.IsActive).Select(x=>x.ReferenceId).SingleAsync();
using var rsa=RSA.Create(2048);var certReq=new CertificateRequest("CN=localhost",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");san.AddIpAddress(System.Net.IPAddress.Loopback);certReq.CertificateExtensions.Add(san.Build());
using var cert=certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5),DateTimeOffset.UtcNow.AddDays(1));
var certPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));var pfx=Path.Combine(output,"runtime.private.pfx");
await File.WriteAllBytesAsync(pfx,cert.Export(X509ContentType.Pfx,certPassword));
await File.WriteAllTextAsync(Path.Combine(output,"runtime.private.cert.pem"),cert.ExportCertificatePem());
await File.WriteAllTextAsync(Path.Combine(output,"runtime.private.key.pem"),rsa.ExportPkcs8PrivateKeyPem());
await Write("runtime.private.json",new{branch,foreign,typeId,password,inspectionIds,severityId,majorId,taskId,api="https://127.0.0.1:7447",origin="https://127.0.0.1:7448"});
await Write("runtime.json",new{database=name,owner,branch,foreign,typeId,httpsApi="https://127.0.0.1:7447",httpsFrontend="https://127.0.0.1:7448",certificateThumbprint=cert.Thumbprint,syntheticGrant="Existing catalog INSPECTION_PERFORM/VIEW grants; no permission changes",masterDataGrantedToHK=false});
await Guard();
var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Path.Combine(backend,"src/HotelOps.Api"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
start.ArgumentList.Add(Path.Combine(backend,"src/HotelOps.Api/bin/Debug/net10.0/HotelOps.Api.dll"));
start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["DOTNET_ENVIRONMENT"]="Development";
start.Environment["ASPNETCORE_URLS"]="https://127.0.0.1:7447";start.Environment["ConnectionStrings__HotelOpsDb"]=connection;
start.Environment["Kestrel__Certificates__Default__Path"]=pfx;start.Environment["Kestrel__Certificates__Default__Password"]=certPassword;
start.Environment["Evidence__PrivateRoot"]=Path.Combine(output,"private-evidence");start.Environment["Logging__LogLevel__Default"]="None";
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