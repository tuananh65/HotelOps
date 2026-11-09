using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Rbac;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
var count=0;void Check(bool v,string name){if(!v)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
foreach(var decision in new[]{PermissionDecision.Denied,PermissionDecision.Restricted,PermissionDecision.Unauthenticated,PermissionDecision.Unavailable,PermissionDecision.Granted}){
 var p=new Permissions(PermissionDecision.Granted,decision);var r=await RoleCatalogAuthority.EvaluateAsync(p,default);Check(r.Decision==decision,"ROLE_MANAGE decision "+decision);Check(p.Calls.SequenceEqual(new[]{"USER_MANAGE","ROLE_MANAGE"}),"global dual permission checks "+decision);
}
var narrow=new Permissions(PermissionDecision.Denied,PermissionDecision.Granted);Check(!(await RoleCatalogAuthority.EvaluateAsync(narrow,default)).IsGranted,"ROLE_MANAGE alone insufficient");Check(narrow.Calls.Count==1,"fail closed on USER_MANAGE denial");
var cancellation=new CancellationToken(true);var cancelled=false;try{await RoleCatalogAuthority.EvaluateAsync(narrow,cancellation);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"cancel before authorization");
var allowed=new Permissions(PermissionDecision.Granted,PermissionDecision.Granted);Check((await RoleCatalogAuthority.EvaluateAsync(allowed,default)).IsGranted,"global read allowed");Check(!(await RoleCatalogAuthority.EvaluateAsync(new Permissions(PermissionDecision.Denied,PermissionDecision.Denied),default)).IsGranted,"next request cannot inherit authority");
foreach(var (authenticated,restricted,mismatch,expected) in new[]{(false,false,false,401),(true,true,false,403),(true,false,true,401)}){
 var h=new DefaultHttpContext();h.Request.Scheme="https";if(authenticated)h.User=new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"7"),new Claim(HotelOpsCookieTicket.SessionIdClaim,"9"),new Claim(HotelOpsCookieTicket.CredentialClaim,new string('A',43)),new Claim(HotelOpsCookieTicket.RestrictedClaim,restricted?"1":"0"),new Claim(HotelOpsCookieTicket.CreatedClaim,DateTime.UtcNow.Ticks.ToString()),new Claim(HotelOpsCookieTicket.VersionClaim,"1")},HotelOpsCookieTicket.Scheme));
 var action=new ActionContext(h,new RouteData(),new ActionDescriptor());var filters=new List<IFilterMetadata>();var context=new ActionExecutingContext(action,filters,new Dictionary<string,object?>(),new object());var invoked=false;var filter=new RoleCatalogReadFilter(null!,new Actor(authenticated,restricted,mismatch),allowed,TimeProvider.System);await filter.OnActionExecutionAsync(context,()=>{invoked=true;throw new Exception("must not query");});Check(!invoked&&context.Result is ObjectResult o&&o.StatusCode==expected,"identity admission before lookup "+expected+" mismatch="+mismatch);
}
var registrations=new ServiceCollection();registrations.AddLogging();registrations.AddSingleton<IWebHostEnvironment>(new Env());registrations.AddControllers().AddApplicationPart(typeof(RoleCatalogController).Assembly);using(var provider=registrations.BuildServiceProvider()){var actions=provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToArray();var ours=actions.Where(a=>a.ControllerTypeInfo.AsType()==typeof(RoleCatalogController)).ToArray();Check(ours.Length==4,"MVC discovers four catalog actions");foreach(var a in ours){Check(a.ActionConstraints!.OfType<HttpMethodActionConstraint>().Single().HttpMethods.SequenceEqual(new[]{"GET"}),"GET only "+a.ActionName);var template=a.AttributeRouteInfo!.Template;Check(actions.Count(x=>x.AttributeRouteInfo?.Template==template&&x.ActionConstraints!.OfType<HttpMethodActionConstraint>().Any(c=>c.HttpMethods.Contains("GET")))==1,"no exact route collision "+template);}}
var now=DateTime.UtcNow;var u=new User{UserId=1,AccountStatus="ACTIVE",EmploymentStatus="ACTIVE"};var role=new Role{RoleId=1,RoleCode="R",RoleName="Role",IsSystemRole=true,IsActive=false};
role.UserRoles.Add(new(){UserId=1,User=u,IsActive=true,AssignedAt=now.AddDays(-1)});role.UserRoles.Add(new(){UserId=1,User=u,IsActive=true,AssignedAt=now.AddDays(-1)});role.UserRoles.Add(new(){UserId=2,User=u,IsActive=true,AssignedAt=now.AddDays(1)});role.UserRoles.Add(new(){UserId=3,User=u,IsActive=true,AssignedAt=now.AddDays(-1),ExpiresAt=now});role.RolePermissions.Add(new());
var mapped=RoleCatalogQueries.Projection(now).Compile()(role);Check(mapped.AssignedUserCount==1,"distinct temporal active assignment count (in-memory)");Check(mapped.IsSystemRole&&!mapped.IsActive&&mapped.PermissionGrantCount==1,"inactive system role + raw grant count mapping");role.IsSystemRole=false;Check(!RoleCatalogQueries.Projection(now).Compile()(role).IsSystemRole,"custom role mapping");u.DeletedAt=now;Check(RoleCatalogQueries.Projection(now).Compile()(role).AssignedUserCount==0,"deleted user excluded");
foreach(var (page,size) in new[]{(0,25),(1,101),(1000001,25)}){var invalid=false;try{RoleCatalogQueries.Validate(page,size,null);}catch(ValidationException){invalid=true;}Check(invalid,"query limits "+page+"/"+size);}
var dto=new RoleDetailDto(mapped,[new(1,new(1,"ROOM_VIEW","Room view","ROOM","room","view",null),"BRANCH")]);var json=JsonSerializer.SerializeToElement(dto,new JsonSerializerOptions(JsonSerializerDefaults.Web));Check(json.GetProperty("grants")[0].GetProperty("dataScope").GetString()=="BRANCH","grant scope DTO mapping");Check(!json.GetProperty("grants")[0].GetProperty("permission").TryGetProperty("isActive",out _),"no invented permission active flag");
var error=RoleCatalogReadFilter.Error(503,"CATALOG_UNAVAILABLE");Check(error.StatusCode==503&&!JsonSerializer.Serialize(error.Value).Contains("connection",StringComparison.OrdinalIgnoreCase),"sanitized unavailable response");
using(var db=new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer().Options)){var sql=db.Roles.AsNoTracking().OrderBy(r=>r.RoleId).Take(25).Select(RoleCatalogQueries.Projection(now)).ToQueryString();Check(sql.Contains("SELECT")&&sql.Contains("COUNT"),"SQL Server projection translates without connecting (not execution)");}
Console.WriteLine($"RESULT: {count} PASS. SQL execution/expired-session/final-gate/concurrent authorization acceptance NOT RUN.");
sealed class Actor(bool present,bool restricted,bool mismatch):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo u){u=new(7,mismatch?10:9,restricted);return present;}}
sealed class Permissions(PermissionDecision user,PermissionDecision role):IPermissionService{public List<string> Calls{get;}=[];public Task<PermissionEvaluation> EvaluateGlobalAsync(string code,CancellationToken ct){Calls.Add(code);return Task.FromResult(new PermissionEvaluation(code=="USER_MANAGE"?user:role));}public Task<PermissionEvaluation> EvaluateBranchAsync(string p,long id,CancellationToken ct)=>throw new Exception("No branch authority substitution");public Task<PermissionEvaluation> EvaluateResourceAsync(string p,PermissionResource r,CancellationToken ct)=>throw new Exception("No resource substitution");}

sealed class Env:IWebHostEnvironment{public string ApplicationName{get;set;}=typeof(RoleCatalogController).Assembly.GetName().Name!;public string EnvironmentName{get;set;}="Development";public string ContentRootPath{get;set;}=Directory.GetCurrentDirectory();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();public string WebRootPath{get;set;}=Directory.GetCurrentDirectory();public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();}