using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Rbac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[NonController] // UC008 candidate: runtime and SQL acceptance required before activation.
[ApiController][Route("api/roles")][Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ValidateAntiForgeryToken][ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class CustomRoleCandidateController(HotelOpsDbContext db,ICurrentUserAccessor current,IPermissionService permissions,TimeProvider clock,IHttpContextAccessor http):ControllerBase
{
 private readonly CustomRoleService service=new(db,current,permissions,clock,http);
 [HttpPost][RequestSizeLimit(32768)]public Task<IActionResult> Create(CustomRoleCreateRequest request,CancellationToken ct)=>Execute(()=>service.CreateAsync(request,ct),ct);
 [HttpPatch("{roleId:long}")][RequestSizeLimit(32768)]public Task<IActionResult> Update(long roleId,CustomRoleUpdateRequest request,CancellationToken ct)=>Execute(()=>service.UpdateAsync(roleId,request,ct),ct);
 private async Task<IActionResult> Execute(Func<Task<CustomRoleResult>> action,CancellationToken ct){Response.Headers.CacheControl="no-store";if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");try{var r=await action();return r.Status==201?Created($"/api/roles/{r.Value!.RoleId}",r.Value):Problem(statusCode:r.Status,title:r.Code);}catch(ValidationException){return Problem(statusCode:400,title:"ROLE_VALIDATION_FAILED");}catch(Exception e)when(!ct.IsCancellationRequested&&e is DbException or DbUpdateException or SessionStoreUnavailableException){return Problem(statusCode:503,title:"ROLE_STORE_UNAVAILABLE");}}
}
