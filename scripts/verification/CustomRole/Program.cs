using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Rbac;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
var count=0;void Check(bool v,string label){if(!v)throw new Exception("FAIL: "+label);count++;Console.WriteLine("PASS: "+label);}
var normalized=new CustomRoleCreateRequest(" custom_x "," Custom name "," ").Normalize();Check(normalized.RoleCode=="CUSTOM_X"&&normalized.RoleName=="Custom name"&&normalized.Description==null,"GBR003 trim/normalize");
foreach(var input in new[]{new CustomRoleCreateRequest("","Name"),new("X"," "),new(new string('X',61),"Name"),new("X",new string('N',151)),new("X\nY","Name")}){var rejected=false;try{input.Normalize();}catch(ValidationException){rejected=true;}Check(rejected,"invalid creation metadata rejected");}
foreach(var pair in new[]{("straße","STRAßE"),("ß","ß"),("ﬀ","ﬀ")})Check(new CustomRoleCreateRequest(pair.Item1,"Name").Normalize().RoleCode==pair.Item2,"invariant role code normalization "+pair.Item1);
var role=CustomRoleService.NewRole(normalized,DateTime.UtcNow);Check(!role.IsSystemRole&&role.IsActive,"only custom active role");Check(role.UserRoles.Count==0&&role.RolePermissions.Count==0,"no grants or assignments even for arbitrary names");Check(CustomRoleService.UpdateBoundary(new Role{IsSystemRole=true}).Status==409,"system metadata protected");Check(CustomRoleService.UpdateBoundary(new Role{IsSystemRole=false}).Code=="ROLE_VERSION_CONTRACT_REQUIRED","custom update fail-closed without version");Check(CustomRoleService.UpdateBoundary(null).Status==404,"missing role boundary");
foreach(var bad in new[]{"{\"roleCode\":\"X\",\"roleName\":\"N\",\"isSystemRole\":true}","{\"roleCode\":\"X\",\"roleName\":\"N\",\"actorUserId\":7}","{\"roleName\":\"N\",\"roleCode\":\"CHANGE\"}"}){var rejected=false;try{if(bad.Contains("CHANGE"))JsonSerializer.Deserialize<CustomRoleUpdateRequest>(bad,new JsonSerializerOptions(JsonSerializerDefaults.Web));else JsonSerializer.Deserialize<CustomRoleCreateRequest>(bad,new JsonSerializerOptions(JsonSerializerDefaults.Web));}catch(JsonException){rejected=true;}Check(rejected,"unknown mutation fields rejected");}
foreach(var restricted in new[]{false,true}){var h=new DefaultHttpContext();if(restricted)h.User=new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"7"),new Claim(HotelOpsCookieTicket.SessionIdClaim,"9"),new Claim(HotelOpsCookieTicket.CredentialClaim,new string('A',43)),new Claim(HotelOpsCookieTicket.RestrictedClaim,"1"),new Claim(HotelOpsCookieTicket.CreatedClaim,DateTime.UtcNow.Ticks.ToString()),new Claim(HotelOpsCookieTicket.VersionClaim,"1")},HotelOpsCookieTicket.Scheme));var service=new CustomRoleService(null!,new Actor(restricted),null!,TimeProvider.System,new HttpContextAccessor{HttpContext=h});Check((await service.CreateAsync(normalized,default)).Status==(restricted?403:401),"create identity before SQL "+restricted);Check((await service.UpdateAsync(1,new("Name"),default)).Status==(restricted?403:401),"update identity before SQL "+restricted);}
var cancelled=false;try{await new CustomRoleService(null!,null!,null!,null!,null!).CreateAsync(normalized,new(true));}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"cancel before SQL");Check(typeof(CustomRoleCandidateController).IsDefined(typeof(NonControllerAttribute),false),"candidate inactive");Check(typeof(CustomRoleCandidateController).IsDefined(typeof(ValidateAntiForgeryTokenAttribute),false),"CSRF metadata retained for candidate");
var http=new DefaultHttpContext();http.Request.Scheme="http";var controller=new CustomRoleCandidateController(null!,null!,null!,null!,null!){ControllerContext=new(){HttpContext=http}};Check(await controller.Create(normalized,default) is ObjectResult{StatusCode:400},"HTTPS before service");
Console.WriteLine($"RESULT: {count} PASS. Authorized SQL create, duplicate/concurrent create, audit atomicity, expired session and rollback NOT RUN.");
sealed class Actor(bool present):ICurrentUserAccessor{public bool TryGetCurrentUser(out CurrentUserInfo actor){actor=new(7,9,true);return present;}}
