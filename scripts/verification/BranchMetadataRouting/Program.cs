using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

var count = 0;
void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); count++; Console.WriteLine("PASS: " + label); }
var manager = new ApplicationPartManager();
manager.ApplicationParts.Add(new AssemblyPart(typeof(BranchMaintenanceController).Assembly));
manager.FeatureProviders.Add(new ControllerFeatureProvider());
var feature = new ControllerFeature(); manager.PopulateFeature(feature);
Check(feature.Controllers.Any(c => c.AsType() == typeof(BranchMaintenanceController)), "metadata controller active");
Check(!feature.Controllers.Any(c => c.AsType() == typeof(BranchAdministrationController)), "administration and deactivate remain undiscovered");
var actions = typeof(BranchMaintenanceController).GetMethods().SelectMany(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().SelectMany(a => a.HttpMethods.Select(v => (Verb:v, Route:a.Template)))).ToArray();
Check(actions.Length == 3 && actions.Contains(("POST", "api/branches")) && actions.Contains(("GET", "api/branches/{id:long}/edit")) && actions.Contains(("PUT", "api/branches/{id:long}")), "exact Create Read Update inventory");
var all = feature.Controllers.SelectMany(c => c.GetMethods()).SelectMany(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().SelectMany(a => a.HttpMethods.Select(v => (v, Route:a.Template)))).ToArray();
foreach(var action in actions) Check(all.Count(a => a.v == action.Verb && a.Route == action.Route) == 1, "no metadata route collision " + action.Verb);
Check(!all.Any(a => a.Route == "api/branches/{id:long}/deactivate"), "no branch deactivation route");
Check(typeof(BranchMaintenanceController).GetCustomAttribute<AuthorizeAttribute>()?.AuthenticationSchemes == HotelOpsCookieTicket.Scheme, "cookie authentication");
Check(typeof(BranchMaintenanceController).GetCustomAttribute<AutoValidateAntiforgeryTokenAttribute>() != null, "unsafe MVC CSRF guard");
Check(typeof(BranchMaintenanceController).GetCustomAttribute<ResponseCacheAttribute>()?.NoStore == true, "no-store");
using var connection = new SqlConnection();
using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer(connection).Options);
var service = new BranchAdministrationService(db, new NoActor(), null!, null!);
var controller = new BranchMaintenanceController(service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
var input = new CreateBranchRequest("TEST", "Name", "Address", null, null);
var update = new UpdateBranchRequest("Name", "Address", null, null, "Valid change reason");
async Task<int?> Status(Task<IActionResult> call) => ((ObjectResult)await call).StatusCode;
Check(await Status(controller.Create(input, default)) == 400, "HTTPS admission");
controller.Request.Scheme = "https";
Check(await Status(controller.Create(input, default)) == 401, "anonymous denied before store");
var ticket = SessionReference.FromProtectedTicket(7, new string('A', 43), false, DateTime.UtcNow);
controller.HttpContext.User = (System.Security.Claims.ClaimsPrincipal)typeof(HotelOpsCookieTicket).GetMethod("BuildPrincipal", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [ticket, 70L])!;
Check(await Status(controller.Update(1, update, default)) == 428, "missing If-Match");
foreach(var bad in new[]{"*", "W/\"AAAAAAAAAAE=\"", "\""+new string('A',64)+"\"", "\"AAAAAAAAAAB=\"", "\"AAAAAAAAAAE=\",\"AAAAAAAAAAI=\""}) {
    controller.Request.Headers.IfMatch = bad;
    Check(await Status(controller.Update(1, update, default)) == 400, "invalid or noncanonical If-Match");
}
controller.Request.Headers.IfMatch = "\"AAAAAAAAAAE=\"";
Check(await Status(controller.Update(1, update, default)) == 401, "valid RowVersion reaches existing service identity gate");
Check(await Status(controller.Read(1, default)) == 401 && await Status(controller.Create(input, default)) == 401, "metadata uses existing service admission");
var parse = typeof(BranchMaintenanceController).GetMethod("Version", BindingFlags.Instance | BindingFlags.NonPublic)!;
for(var i=0;i<256;i++) {
    var bytes=new byte[]{0,1,2,3,4,5,6,(byte)i}; controller.Request.Headers.IfMatch='"'+Convert.ToBase64String(bytes)+'"';
    if(!((byte[])parse.Invoke(controller,null)!).SequenceEqual(bytes))throw new Exception("RowVersion mismatch");
}
Check(true, "all canonical final-byte RowVersions roundtrip");
var respond = typeof(BranchMaintenanceController).GetMethod("ResponseFor", BindingFlags.Instance | BindingFlags.NonPublic)!;
foreach(var status in new[]{200,201,400,401,403,404,409,412,428,503}) {
    controller.Response.Headers.Clear();
    var result=(ObjectResult)respond.Invoke(controller,[new BranchAdministrationResult(status, status<400 ? new EditableBranchDto(1,"TEST","Name","Address",null,null,"Asia/Ho_Chi_Minh",true) : null,"SAFE_CODE",status<400?"\"AAAAAAAAAAE=\"":null)])!;
    Check(result.StatusCode==status && (status>=400 || controller.Response.Headers.ETag=="\"AAAAAAAAAAE=\""), "existing service status/ETag " + status);
}
foreach(var method in new[]{"POST","PUT"}) {
    var reached=false;var middleware=new ApiCsrfMiddleware(_=>{reached=true;return Task.CompletedTask;});
    var ctx=new DefaultHttpContext();ctx.Request.Path="/api/branches";ctx.Request.Method=method;ctx.Response.Body=new MemoryStream();
    await middleware.InvokeAsync(ctx,null!);
    Check(!reached && ctx.Response.StatusCode==400,"global missing-CSRF blocks " + method);
}
foreach(var body in new[]{"{\"code\":\"B\",\"name\":\"N\",\"address\":\"A\",\"isActive\":false}","{\"code\":\"B\",\"name\":\"N\",\"address\":\"A\",\"timezone\":\"UTC\"}"}) {
    try { JsonSerializer.Deserialize<CreateBranchRequest>(body,new JsonSerializerOptions(JsonSerializerDefaults.Web));throw new Exception("Overposting accepted"); }
    catch(JsonException){Check(true,"strict create DTO rejects server-owned fields");}
}
Check(connection.ConnectionString=="" && connection.State==System.Data.ConnectionState.Closed && !db.ChangeTracker.Entries().Any(),"no SQL connection or writes");
Console.WriteLine($"{count}/{count} PASS; SQL rowversion, permission, audit atomicity and races NOT_RUN.");
sealed class NoActor : ICurrentUserAccessor { public bool TryGetCurrentUser(out CurrentUserInfo actor) {actor=default;return false;} }
