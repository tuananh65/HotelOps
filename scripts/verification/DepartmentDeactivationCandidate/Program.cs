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
Console.WriteLine($"{count}/{count} checks PASS. SQL mutation/dependency/rollback NOT_RUN.");
sealed class Actor:ICurrentUserAccessor{public CurrentUserInfo Value;public bool TryGetCurrentUser(out CurrentUserInfo actor){actor=Value;return true;}}
