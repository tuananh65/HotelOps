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
var active=new CreatedRoomDto(10,1,2,null,3,"101",null,"DIRTY",true);
var off=RoomLifecycleRules.Plan(active,false,true,true,true,true);Check(!off.AfterActive&&off.RequiredCondition==RoomLifecycleCondition.Inactive,"deactivation intent Inactive");
var on=RoomLifecycleRules.Plan(active with{IsActive=false},true,true,true,true,true);Check(on.AfterActive&&on.RequiredCondition==RoomLifecycleCondition.PendingCleaning,"reactivation never plans Ready");Check(off.RequiresAtomicAudit&&off.RequiresStatusHistory&&on.RequiresAtomicAudit&&on.RequiresStatusHistory,"status history and audit atomic obligation explicit");
foreach(var parents in new[]{(false,true,true,true),(true,false,true,true),(true,true,false,true),(true,true,true,false)})try{RoomLifecycleRules.Plan(active with{IsActive=false},true,parents.Item1,parents.Item2,parents.Item3,parents.Item4);throw new Exception("invalid parent accepted");}catch(ValidationException){Check(true,"reactivation invalid parent denied");}
foreach(var pair in new[]{(active,true),(active with{IsActive=false},false)})try{RoomLifecycleRules.Plan(pair.Item1,pair.Item2,true,true,true,true);throw new Exception("duplicate accepted");}catch(ValidationException){Check(true,"duplicate state transition denied");}
foreach(var request in new[]{new RoomLifecycleRequest("short",true),new RoomLifecycleRequest("valid reason text",false),new RoomLifecycleRequest("bad\nreasontext",true),new RoomLifecycleRequest(new string('x',1001),true)})try{request.ValidatedReason();throw new Exception("invalid reason");}catch(ValidationException){Check(true,"reason confirmation validation");}
Check(new RoomLifecycleRequest("  valid reason text  ",true).ValidatedReason()=="valid reason text","reason snapshot normalization");
try{JsonSerializer.Deserialize<RoomLifecycleRequest>("{\"reason\":\"valid reason text\",\"confirmation\":true,\"operationalStatus\":\"READY\"}",new JsonSerializerOptions(JsonSerializerDefaults.Web));throw new Exception("overpost accepted");}catch(JsonException){Check(true,"client status overpost denied");}
var manager=new ApplicationPartManager();manager.ApplicationParts.Add(new AssemblyPart(typeof(RoomLifecycleCandidateController).Assembly));manager.FeatureProviders.Add(new ControllerFeatureProvider());var feature=new ControllerFeature();manager.PopulateFeature(feature);Check(!feature.Controllers.Any(c=>c.AsType()==typeof(RoomLifecycleCandidateController)),"room candidate absent from actual MVC discovery");Check(!feature.Controllers.Any(c=>c.Name=="WorkdaysController"),"Workday writes remain inactive");
using var connection=new SqlConnection();using var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);var actor=new Actor{Value=new(7,70,false)};var service=new RoomTypeCreationService(db,actor,null!,null!,TimeProvider.System);var ticket=SessionReference.FromProtectedTicket(7,new string('A',43),false,DateTime.UtcNow);var tag='"'+new string('A',64)+'"';var valid=new RoomLifecycleRequest("Valid room reason",true);
foreach(var mode in new[]{false,true})Check((await service.ChangeRoomLifecycleCandidateAsync(ticket,70,1,10,mode,tag,valid,default)).Status==RoomTypeBatchStatus.Unavailable,"both direct operations fail closed before SQL");Check(connection.State==System.Data.ConnectionState.Closed&&connection.ConnectionString==""&&!db.ChangeTracker.Entries().Any(),"no DB connection/mutation/history/audit before release");
actor.Value=new(8,70,false);Check((await service.ChangeRoomLifecycleCandidateAsync(ticket,70,1,10,false,tag,valid,default)).Status==RoomTypeBatchStatus.SessionInvalid,"actor mismatch denied");actor.Value=new(7,70,true);Check((await service.ChangeRoomLifecycleCandidateAsync(ticket,70,1,10,true,tag,valid,default)).Status==RoomTypeBatchStatus.SessionInvalid,"restricted denied");actor.Value=new(7,70,false);
var concurrent=await Task.WhenAll(service.ChangeRoomLifecycleCandidateAsync(ticket,70,1,10,false,tag,valid,default),service.ChangeRoomLifecycleCandidateAsync(ticket,70,1,10,true,tag,valid,default));Check(concurrent.All(r=>r.Status==RoomTypeBatchStatus.Unavailable),"concurrent opposite operations cannot bypass release");
try{((IList<string>)RoomTypeCreationService.RoomLifecycleReleaseGates).Clear();throw new Exception("mutable gates");}catch(NotSupportedException){Check(true,"immutable dependency/status/SQL release gates");}
var context=new DefaultHttpContext();var controller=new RoomLifecycleCandidateController(service){ControllerContext=new ControllerContext{HttpContext=context}};async Task<int?> Status()=>((ObjectResult)await controller.Deactivate(10,1,valid,default)).StatusCode;
Check(await Status()==400,"HTTPS required");context.Request.Scheme="https";Check(await Status()==401,"cookie required");context.User=(System.Security.Claims.ClaimsPrincipal)typeof(HotelOpsCookieTicket).GetMethod("BuildPrincipal",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[ticket,70L])!;Check(await Status()==428,"If-Match required");context.Request.Headers.IfMatch="\"0011223344556677\"";Check(await Status()==400,"User rowversion cannot replace Room ETag");context.Request.Headers.IfMatch=tag;Check(await Status()==503,"valid candidate request stays unavailable");
Console.WriteLine($"{count}/{count} checks PASS. SQL/status mapping/rollback NOT_RUN.");
sealed class Actor:ICurrentUserAccessor{public CurrentUserInfo Value;public bool TryGetCurrentUser(out CurrentUserInfo actor){actor=Value;return true;}}
