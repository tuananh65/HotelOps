using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

if(args.Length!=1 || File.Exists(args[0]))throw new ArgumentException("New receipt path required");
var checks=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
var services=new ServiceCollection();
services.AddLogging();services.AddHttpContextAccessor();services.AddSingleton<TimeProvider>(TimeProvider.System);
// This runner never opens a connection; no local/user-secrets configuration is loaded.
services.AddDbContext<HotelOpsDbContext>(o=>o.UseSqlServer("Server=unused;Database=unused;Integrated Security=true"));
services.AddScoped<ICurrentUserAccessor,CurrentUserAccessor>();
services.AddScoped<IPermissionDataSource,EfPermissionDataSource>();
services.AddScoped<ICleaningTaskAccessReader,EfCleaningTaskAccessReader>();
services.AddScoped<IPermissionResourceResolver>(sp=>new WorkdayPermissionResourceResolver(sp.GetRequiredService<HotelOpsDbContext>(),ActivatorUtilities.CreateInstance<CleaningTaskResourceResolver>(sp)));
services.AddScoped<IPermissionService,PermissionService>();
services.AddScoped<ISessionRevocationService,SessionRevocationService>();services.AddScoped<ISqlSessionService,SqlSessionService>();
services.AddScoped<IWorkTimeTransactionParticipant,SqlWorkTimeTransactionParticipant>();
services.AddScoped<SqlWorkTimeTransactionParticipant>(sp=>(SqlWorkTimeTransactionParticipant)sp.GetRequiredService<IWorkTimeTransactionParticipant>());
services.AddScoped<OwnWorkdaySnapshotReader>();services.AddScoped<WorkdayService>();
services.AddScoped<RoomTypeDirectoryService>();services.AddScoped<TurnoverHistoryService>();
services.AddScoped<BranchAdministrationService>(); // Proposed exact registration; production Program untouched.
services.AddScoped<BranchAdministrationController>();
using var provider=services.BuildServiceProvider(new ServiceProviderOptions{ValidateOnBuild=true,ValidateScopes=true});
using var a=provider.CreateScope();using var b=provider.CreateScope();
var service=a.ServiceProvider.GetRequiredService<BranchAdministrationService>();
Check(ReferenceEquals(service,a.ServiceProvider.GetRequiredService<BranchAdministrationService>()),"Branch service scoped instance resolves");
Check(!ReferenceEquals(service,b.ServiceProvider.GetRequiredService<BranchAdministrationService>()),"Branch scopes isolated");
Check(ReferenceEquals(a.ServiceProvider.GetRequiredService<IWorkTimeTransactionParticipant>(),a.ServiceProvider.GetRequiredService<SqlWorkTimeTransactionParticipant>()),"A1 interface/concrete retain same scoped instance");
_ = a.ServiceProvider.GetRequiredService<WorkdayService>();
_ = a.ServiceProvider.GetRequiredService<RoomTypeDirectoryService>();
_ = a.ServiceProvider.GetRequiredService<TurnoverHistoryService>();
Check(true,"Existing Workday/UC142/UC038 service graph resolves");
var db=a.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
var field=typeof(BranchAdministrationService).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(HotelOpsDbContext));
Check(ReferenceEquals(field.GetValue(service),db),"Branch service shares scoped DbContext");
var projection=(IQueryable)typeof(BranchAdministrationService).GetMethod("BranchRows",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(service,null)!;
var sql=projection.ToQueryString();
Check(sql.Contains("row_version") && sql.Contains("UPDLOCK,HOLDLOCK"),"Unmapped SQL-rowversion projection translates without changing shared EF model");
Check(db.Model.FindEntityType(typeof(HotelOps.Api.Entities.Branch))!.FindProperty("RowVersion")==null,"Generated Branch EF model unchanged");
foreach(var input in new CreateBranchRequest[]{new("","N","A",null,null),new("x"," ","A",null,null),new("x","N"," ",null,null),new("x","N","A","bad",null),new("x","N","A",null,"bad"),new("x!","N","A",null,null),new(new string('x',31),"N","A",null,null),new("x","N",new string('a',4001),null,null)})
{
    try{await service.CreateAsync(null!,0,input,default);throw new Exception("Invalid create passed");}
    catch(ValidationException){checks.Add("Invalid create rejected before SQL: "+checks.Count);}
}
try{await service.UpdateAsync(null!,0,1,new byte[7],new("N","A",null,null,"valid reason"),default);throw new Exception("Invalid version passed");}
catch(ValidationException){checks.Add("Invalid SQL rowversion rejected before SQL");}
try{await service.UpdateAsync(null!,0,1,new byte[8],new("N","A",null,null,"short"),default);throw new Exception("Invalid reason passed");}
catch(ValidationException){checks.Add("Update reason contract rejected before SQL");}
try{await service.DeactivateAsync(null!,0,1,new byte[8],new("valid reason",false),default);throw new Exception("Missing confirmation passed");}
catch(ValidationException){checks.Add("Deactivation confirmation required before SQL");}
foreach(var (kind,status,expected) in new[]{("operational_tasks","COMPLETED","CLOSED"),("operational_tasks","TODO","OPEN"),("incidents","RESOLVED","UNRESOLVED"),("incidents","CLOSED","CLOSED"),("material_requests","APPROVED","OPEN"),("material_requests","FULLY_ISSUED","UNRESOLVED"),("maintenance_scope_mismatch","CLOSED","UNRESOLVED"),("unknown","OPEN","UNRESOLVED"),("replenishments","CLOSED","CLOSED")})
    Check(BranchDeactivationDependencyReader.Classify(kind,status)==expected,$"Dependency classifier {kind}/{status}: {expected}");
Check(!new BranchDependencySnapshot(1,0,0,[],false,true).CanDeactivate,"Empty dependency snapshot does not authorize deactivation");
var controller=a.ServiceProvider.GetRequiredService<BranchAdministrationController>();
controller.ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()};
Check(await controller.Read(1,default) is ObjectResult{StatusCode:400},"Controller rejects plaintext before SQL");
controller.Request.Scheme="https";
Check(await controller.Read(1,default) is ObjectResult{StatusCode:401},"Controller rejects anonymous HTTPS before SQL");
var tag=typeof(BranchAdministrationController).GetMethod("Tag",BindingFlags.Instance|BindingFlags.NonPublic)!;
foreach(var value in new[]{"*","W/\"AAAAAAAAAAA=\"","\"AAAAAAAAAA==\"","\"AAAAAAAAAAB=\"","\"AAAAAAAAAAA=\",\"AAAAAAAAAAA=\""})
{
    controller.Request.Headers.IfMatch=value;
    try{tag.Invoke(controller,null);throw new Exception("Invalid If-Match accepted");}
    catch(TargetInvocationException e)when(e.InnerException is ValidationException){checks.Add("Strict If-Match rejects "+value);}
}
controller.Request.Headers.IfMatch="\"AAAAAAAAAAA=\"";
Check(((byte[])tag.Invoke(controller,null)!).Length==8,"Canonical SQL rowversion accepted");
try{JsonSerializer.Deserialize<CreateBranchRequest>("{\"Code\":\"X\",\"Name\":\"N\",\"Address\":\"A\",\"isActive\":false}");throw new Exception("Client status accepted");}
catch(JsonException){checks.Add("Create DTO rejects client-owned status");}
try{JsonSerializer.Deserialize<UpdateBranchRequest>("{\"Name\":\"N\",\"Address\":\"A\",\"Reason\":\"valid reason\",\"Code\":\"X\"}");throw new Exception("Client code accepted");}
catch(JsonException){checks.Add("Update DTO rejects branch code mutation");}
Check(!db.ChangeTracker.Entries().Any(),"No entities tracked or business writes staged");
await File.WriteAllTextAsync(args[0],JsonSerializer.Serialize(new{success=true,checks=checks.Count,cases=checks,scope="PROPOSED_DI_BOUNDARY_EF_TRANSLATION_NO_SQL_NO_HTTPS_SERVER",productionDiApplied=false,sqlConnections=0},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"PASS {checks.Count} DI/boundary/translation checks; real HTTPS/SQL NOT RUN");
