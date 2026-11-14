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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
var count=0;void Check(bool v,string name){if(!v)throw new Exception("FAIL "+name);count++;Console.WriteLine("PASS "+name);}
var manager=new ApplicationPartManager();manager.ApplicationParts.Add(new AssemblyPart(typeof(DepartmentDeactivationCandidateController).Assembly));manager.FeatureProviders.Add(new ControllerFeatureProvider());var feature=new ControllerFeature();manager.PopulateFeature(feature);
Check(!feature.Controllers.Any(c=>c.AsType()==typeof(DepartmentDeactivationCandidateController)),"Department mutation absent from actual MVC discovery");Check(!feature.Controllers.Any(c=>c.Name=="WorkdaysController"),"Workday writes remain inactive");
Check(typeof(DepartmentDeactivationCandidateController).GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute),true).Length==1,"CSRF metadata retained");
foreach(var body in new[]{"{\"reason\":\"valid reason text\",\"confirmation\":true,\"isActive\":false}","{\"reason\":\"valid reason text\",\"confirmation\":true,\"actorId\":7}"})try{JsonSerializer.Deserialize<DeactivateStructureRequest>(body,new JsonSerializerOptions(JsonSerializerDefaults.Web));throw new Exception("overpost accepted");}catch(JsonException){Check(true,"strict existing DTO rejects overposting");}
using var connection=new SqlConnection();using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);var actor=new Actor{Value=new(7,70,false)};var service=new RoomTypeCreationService(db,actor,null!,null!,TimeProvider.System);var ticket=SessionReference.FromProtectedTicket(7,new string('A',43),false,DateTime.UtcNow);var tag='"'+new string('A',64)+'"';var valid=new DeactivateStructureRequest("Valid department reason",true);
async Task<RoomTypeBatchResult> Call(DeactivateStructureRequest? request=null,long branch=1,long id=2,string? etag=null,CancellationToken ct=default)=>await service.DeactivateDepartmentCandidateAsync(ticket,70,branch,id,etag??tag,request??valid,ct);
Check((await Call()).Status==RoomTypeBatchStatus.Unavailable,"direct service remains fail-closed");Check(connection.State==System.Data.ConnectionState.Closed&&connection.ConnectionString==""&&!db.ChangeTracker.Entries().Any(),"no SQL connection or mutation before acceptance");
foreach(var request in new[]{new DeactivateStructureRequest("short",true),new DeactivateStructureRequest("Valid department reason",false),new DeactivateStructureRequest("Invalid\nreason",true),new DeactivateStructureRequest(new string('x',1001),true)})try{await Call(request);throw new Exception("invalid request accepted");}catch(ValidationException){Check(true,"reason confirmation guard");}
foreach(var bad in new[]{"*","\"0011223344556677\""})try{await Call(etag:bad);throw new Exception("bad tag accepted");}catch(ValidationException){Check(true,"wrong ETag denied");}
try{await Call(branch:0);throw new Exception("bad scope accepted");}catch(ValidationException){Check(true,"positive branch binding required");}
actor.Value=new(8,70,false);Check((await Call()).Status==RoomTypeBatchStatus.SessionInvalid,"actor mismatch denied");actor.Value=new(7,71,false);Check((await Call()).Status==RoomTypeBatchStatus.SessionInvalid,"session replacement denied");actor.Value=new(7,70,true);Check((await Call()).Status==RoomTypeBatchStatus.SessionInvalid,"restricted denied");actor.Value=new(7,70,false);
try{await Call(ct:new CancellationToken(true));throw new Exception("cancellation ignored");}catch(OperationCanceledException){Check(true,"cancellation before SQL");}
var results=await Task.WhenAll(Call(),Call());Check(results.All(r=>r.Status==RoomTypeBatchStatus.Unavailable),"duplicate direct calls never activate candidate");
try{((IList<string>)RoomTypeCreationService.DepartmentDeactivationReleaseGates).Clear();throw new Exception("mutable gates");}catch(NotSupportedException){Check(true,"immutable dependency/writer/audit/SQL gates");}
var context=new DefaultHttpContext();var controller=new DepartmentDeactivationCandidateController(service){ControllerContext=new ControllerContext{HttpContext=context}};async Task<int?> Status()=>((ObjectResult)await controller.Deactivate(1,2,valid,default)).StatusCode;
Check(await Status()==400,"HTTPS required");context.Request.Scheme="https";Check(await Status()==401,"cookie required");context.User=(System.Security.Claims.ClaimsPrincipal)typeof(HotelOpsCookieTicket).GetMethod("BuildPrincipal",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[ticket,70L])!;Check(await Status()==428,"If-Match required");context.Request.Headers.IfMatch="*";Check(await Status()==400,"malformed If-Match rejected");context.Request.Headers.IfMatch=tag;Check(await Status()==503,"valid candidate HTTP remains unavailable");
// Execute the production dependency decision and conditional-update predicate without SQL.
var decisionMethod = typeof(RoomTypeCreationService).GetMethod("DepartmentDeactivationDecision", BindingFlags.NonPublic | BindingFlags.Static)!;
RoomTypeBatchStatus Decision(HotelOps.Api.Services.Dependencies.DependencySnapshot snapshot, bool users = false, bool warehouses = false, bool coverage = false)
    => (RoomTypeBatchStatus)decisionMethod.Invoke(null, [snapshot, users, warehouses, coverage])!;
var clear = new HotelOps.Api.Services.Dependencies.DependencySnapshot("DEPARTMENT", 2, 1, [], [], "synthetic", true);
Check(Decision(clear) == RoomTypeBatchStatus.Unavailable, "empty reader never substitutes for owner coverage");
Check(Decision(clear, coverage: true) == RoomTypeBatchStatus.Success, "fully certified synthetic decision admits writer; not SQL acceptance");
Check(Decision(clear with { CanDeactivate = false }, coverage: true) == RoomTypeBatchStatus.Unavailable, "reader clearance required independently");
Check(Decision(clear with { Unresolved = ["unresolved"] }, coverage: true) == RoomTypeBatchStatus.Unavailable, "unresolved facts fail closed");
Check(Decision(clear, users: true, coverage: true) == RoomTypeBatchStatus.DependenciesExist, "active user blocks deactivation");
Check(Decision(clear, warehouses: true, coverage: true) == RoomTypeBatchStatus.DependenciesExist, "warehouse blocks without deleting references");
foreach (var classification in Enum.GetValues<HotelOps.Api.Services.Dependencies.DependencyClassification>())
{
    var snapshot = clear with { Facts = [new("ASSET", 9, 1, "synthetic", classification)] };
    var expectedStatus = classification switch
    {
        HotelOps.Api.Services.Dependencies.DependencyClassification.HISTORICAL => RoomTypeBatchStatus.Success,
        HotelOps.Api.Services.Dependencies.DependencyClassification.UNKNOWN_POLICY => RoomTypeBatchStatus.Unavailable,
        _ => RoomTypeBatchStatus.DependenciesExist
    };
    Check(Decision(snapshot, coverage: true) == expectedStatus, "classification preserved: " + classification);
}
var predicateMethod = typeof(RoomTypeCreationService).GetMethod("DepartmentDeactivationRows", BindingFlags.NonPublic | BindingFlags.Static)!;
var before = new DepartmentInformationDto(2, 1, "D", "Department", null, null, true);
IQueryable<HotelOps.Api.Entities.Department> Conditional(IQueryable<HotelOps.Api.Entities.Department> rows)
    => (IQueryable<HotelOps.Api.Entities.Department>)predicateMethod.Invoke(null, [rows, before])!;
HotelOps.Api.Entities.Department Row() => new() { DepartmentId = 2, BranchId = 1, DepartmentCode = "D", DepartmentName = "Department", IsActive = true };
Check(Conditional(new[] { Row() }.AsQueryable()).Count() == 1, "exact active snapshot eligible for conditional write");
foreach (var change in new Action<HotelOps.Api.Entities.Department>[] { r => r.DepartmentId++, r => r.BranchId++, r => r.IsActive = false,
    r => r.DepartmentCode = "OTHER", r => r.DepartmentName = "Changed", r => r.ManagerUserId = 9, r => r.ScopeDescription = "changed" })
{
    var stale = Row(); change(stale);
    Check(!Conditional(new[] { stale }.AsQueryable()).Any(), "changed identity/state/field cannot match conditional write");
}
var translated = Conditional(db.Departments).ToQueryString();
Check(translated.Contains("[department_id]") && translated.Contains("[branch_id]") && translated.Contains("[is_active]")
    && translated.Contains("[manager_user_id] IS NULL") && translated.Contains("[scope_description] IS NULL"), "SQL null-safe snapshot predicate translates offline; execution NOT_RUN");
var writer = File.ReadAllText("src/HotelOps.Api/Services/HotelStructure/DepartmentDeactivationCandidateService.cs");
Check(writer.IndexOf("RevalidateInCallerTransactionAsync", StringComparison.Ordinal) < writer.IndexOf(".ExecuteUpdateAsync", StringComparison.Ordinal), "source wiring: retained fingerprint reread before update");
Check(writer.Contains("if (affected != 1)") && writer.Contains("BatchAuditAsync") && writer.Contains("with { IsActive = false }")
    && writer.Contains("ownerCoverageComplete: false"), "source wiring: conditional persistence, audit and incomplete coverage retained");
Console.WriteLine($"{count}/{count} checks PASS. SQL mutation/dependency/rollback NOT_RUN.");
sealed class Actor:ICurrentUserAccessor{public CurrentUserInfo Value;public bool TryGetCurrentUser(out CurrentUserInfo actor){actor=Value;return true;}}
