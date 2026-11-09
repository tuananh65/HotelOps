using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
var checks=0;
void Check(bool condition,string label){if(!condition)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
var controller=typeof(HotelStructureUpdateController);
foreach(var (name,route) in new[]{("FloorDeactivationDependencies","floors/{id}/deactivation-dependencies"),("AreaDeactivationDependencies","areas/{id}/deactivation-dependencies")}){
 var m=controller.GetMethod(name)!;
 Check(m.GetCustomAttribute<HttpGetAttribute>()?.Template==route,"GET route "+name);
 Check(m.GetCustomAttributes<HttpMethodAttribute>().Single().HttpMethods.SequenceEqual(new[]{"GET"}),"GET only "+name);
 Check(m.GetParameters().Where(p=>p.ParameterType==typeof(long)).All(p=>p.GetCustomAttribute<RangeAttribute>() is not null),"positive route IDs "+name);
 Check(m.GetParameters().Last().ParameterType==typeof(CancellationToken),"cancellation "+name);
}
Check(controller.GetCustomAttribute<AuthorizeAttribute>()?.AuthenticationSchemes==HotelOpsCookieTicket.Scheme,"existing cookie scheme");
Check(controller.GetCustomAttribute<ResponseCacheAttribute>()?.NoStore==true,"no-store");
Check(!controller.IsDefined(typeof(AllowAnonymousAttribute)),"not anonymous");
var record=new EditableStructureDto("floor",11,42,"F11","Floor",null,null,2,true);
var preview=new StructureDeactivationPreviewDto(record,[new("ACTIVE_ROOM",2,[1,2])],true);
var tag='"'+new string('A',64)+'"';
async Task<(IActionResult Result,DefaultHttpContext Context)> Respond(string scheme,bool valid,StructureUpdateResult result){
 var c=new HotelStructureUpdateController(null!);var h=new DefaultHttpContext();h.Request.Scheme=scheme;
 if(valid) h.User=new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"7"),new Claim(HotelOpsCookieTicket.SessionIdClaim,"9"),new Claim(HotelOpsCookieTicket.CredentialClaim,new string('A',43)),new Claim(HotelOpsCookieTicket.RestrictedClaim,"0"),new Claim(HotelOpsCookieTicket.CreatedClaim,DateTime.UtcNow.Ticks.ToString()),new Claim(HotelOpsCookieTicket.VersionClaim,"1")},HotelOpsCookieTicket.Scheme));
 c.ControllerContext=new(){HttpContext=h};var execute=controller.GetMethod("Execute",BindingFlags.NonPublic|BindingFlags.Instance)!;
 Func<SessionReference,long,Task<StructureUpdateResult>> work=(_,_)=>Task.FromResult(result);
 return(await (Task<IActionResult>)execute.Invoke(c,[work,false])!,h);
}
Check((await Respond("http",false,new(StructureUpdateStatus.Success))).Result is ObjectResult{StatusCode:400},"HTTPS required before work");
Check((await Respond("https",false,new(StructureUpdateStatus.Success))).Result is ObjectResult{StatusCode:401},"invalid ticket denied before work");
var success=await Respond("https",true,new(StructureUpdateStatus.Success,ETag:tag,Preview:preview));
Check(success.Result is OkObjectResult ok&&ReferenceEquals(ok.Value,preview),"preview whitelist response");
Check(success.Context.Response.Headers.ETag==tag,"real structure tag forwarded");
foreach(var (status,expected) in new[]{(StructureUpdateStatus.SessionInvalid,401),(StructureUpdateStatus.Forbidden,403),(StructureUpdateStatus.NotFound,404),(StructureUpdateStatus.Unavailable,503)})Check((await Respond("https",true,new(status))).Result is ObjectResult e&&e.StatusCode==expected,"failure mapping "+expected);
var old=await Respond("https",true,new(StructureUpdateStatus.Success,record,tag));Check(old.Result is OkObjectResult o&&ReferenceEquals(o.Value,record),"legacy record response preserved");
var json=JsonSerializer.SerializeToElement(preview,new JsonSerializerOptions(JsonSerializerDefaults.Web));Check(json.GetProperty("writerCoordinationRequired").GetBoolean(),"readiness remains closed");Check(json.GetProperty("dependencies")[0].GetProperty("count").GetInt64()==2,"count DTO camelcase");
// Static assertions supplement adapter tests; they do not certify SQL isolation or authorization.
var source=File.ReadAllText("src/HotelOps.Api/Services/HotelStructure/HotelStructureUpdateService.cs");
var start=source.IndexOf("            if (preview)",StringComparison.Ordinal);var end=source.IndexOf("            if (deactivate)",start,StringComparison.Ordinal);var block=source[start..end];
Check(block.Contains("StructureDeactivationDependencies.ReadAsync"),"existing scoped dependency reader reused");Check(block.Contains("await Gate()")&&block.IndexOf("await Gate()",StringComparison.Ordinal)<block.IndexOf("tx.CommitAsync",StringComparison.Ordinal),"final gate before read commit");Check(!block.Contains("SaveChanges")&&!block.Contains("ExecuteUpdate")&&!block.Contains("AuditLog"),"preview does not mutate structure or audit");Check(block.Contains("WriterCoordinationRequired: true"),"no readiness certificate");
Check(source.Contains("StructureUpdateStatus.WriterCoordinationRequired, ETag: tag"),"deactivation remains fail-closed");
Console.WriteLine($"RESULT: {checks} PASS. SQL/live authorization/races NOT RUN.");
