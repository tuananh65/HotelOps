using HotelOps.Api.Controllers;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;
using System.Text.Json;
var count=0;void Check(bool v,string label){if(!v)throw new Exception("FAIL "+label);count++;Console.WriteLine("PASS "+label);}
var manager=new ApplicationPartManager();manager.ApplicationParts.Add(new AssemblyPart(typeof(UserDeactivationCandidateController).Assembly));manager.FeatureProviders.Add(new ControllerFeatureProvider());var feature=new ControllerFeature();manager.PopulateFeature(feature);
Check(!feature.Controllers.Any(c=>c.AsType()==typeof(UserDeactivationCandidateController)),"candidate absent from actual MVC discovery");
Check(!feature.Controllers.Any(c=>c.Name=="WorkdaysController"),"Workday mutation remains inactive");
Check(typeof(UserDeactivationCandidateController).GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute),true).Length==1,"candidate has CSRF metadata");
Check(typeof(UserDeactivationCandidateController).GetMethod("Deactivate")!.GetCustomAttribute<HttpPatchAttribute>()!.Template=="{userId:long}/deactivate","proposed PATCH route isolated");
var context=new DefaultHttpContext();var controller=new UserDeactivationCandidateController(null!){ControllerContext=new ControllerContext{HttpContext=context}};
using var valid=JsonDocument.Parse("{\"reason\":\"  Valid departure reason  \"}");
async Task<int?> Status(JsonElement? body=null,long target=8)=>((ObjectResult)await controller.Deactivate(target,body??valid.RootElement,default)).StatusCode;
Check(await Status()==400,"HTTP denied");context.Request.Scheme="https";Check(await Status()==401,"missing cookie denied");
void Identity(bool restricted=false){var ticket=SessionReference.FromProtectedTicket(7,new string('A',43),restricted,DateTime.UtcNow);context.User=(System.Security.Claims.ClaimsPrincipal)typeof(HotelOpsCookieTicket).GetMethod("BuildPrincipal",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[ticket,70L])!;}
Identity();Check(await Status(target:7)==403,"self deactivation denied");Identity(true);Check(await Status()==403,"restricted cookie denied");Identity();Check(await Status()==428,"If-Match required");
foreach(var tag in new[]{"*","W/\"0011223344556677\"","\"aabbccddeeff0011\"","\"001122334455667\""}){context.Request.Headers.IfMatch=tag;Check(await Status()==400,"malformed User tag denied");}
context.Request.Headers.IfMatch="\"0011223344556677\"";
foreach(var json in new[]{"{}","[]","{\"reason\":\"short\"}","{\"reason\":\"valid reason text\",\"actorId\":7}","{\"reason\":\"valid reason text\",\"reason\":\"duplicate\"}"}){using var bad=JsonDocument.Parse(json);Check(await Status(bad.RootElement)==400,"strict existing reason DTO retained");}
Check(await Status()==503,"valid candidate cannot reach service while immutable release gates pending");
Check(await Status()==503,"repeated candidate request cannot bypass gate");
Console.WriteLine($"{count}/{count} candidate checks PASS. SQL success/rollback NOT_RUN.");
