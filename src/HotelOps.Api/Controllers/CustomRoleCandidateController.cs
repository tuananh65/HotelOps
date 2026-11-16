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
[ApiController][Route("api/roles")][Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class CustomRoleCandidateController(HotelOpsDbContext db,ICurrentUserAccessor current,IPermissionService permissions,TimeProvider clock,IHttpContextAccessor http):ControllerBase
{
 private readonly CustomRoleService service=new(db,current,permissions,clock,http);
 [HttpPost][ValidateAntiForgeryToken][RequestSizeLimit(32768)]public Task<IActionResult> Create(CustomRoleCreateRequest request,CancellationToken ct)=>Execute(()=>service.CreateAsync(request,ct),ct);
 [HttpGet("{roleId:long}/metadata")]public Task<IActionResult> Metadata(long roleId,CancellationToken ct)=>Execute(()=>service.ReadMetadataAsync(roleId,ct),ct);
 [HttpPatch("{roleId:long}")][ValidateAntiForgeryToken][RequestSizeLimit(32768)]
 public Task<IActionResult> Update(long roleId,CustomRoleUpdateRequest request,CancellationToken ct)
  => WithVersion(version=>service.UpdateAsync(roleId,request,version,ct),ct);
 [HttpPost("{roleId:long}/deactivate")][ValidateAntiForgeryToken][RequestSizeLimit(1024)]
 public Task<IActionResult> Deactivate(long roleId,CancellationToken ct)
  => WithVersion(version=>service.DeactivateAsync(roleId,version,ct),ct);
 private Task<IActionResult> WithVersion(Func<byte[],Task<CustomRoleResult>> action,CancellationToken ct)
 {
  if(!Request.Headers.TryGetValue("If-Match",out var header))return Task.FromResult<IActionResult>(Problem(statusCode:428,title:"ROLE_VERSION_REQUIRED"));
  var tag=header.ToString();
  if(header.Count!=1||tag.Length!=18||tag[0]!='"'||tag[^1]!='"'||!tag.AsSpan(1,16).ToString().All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F'))return Task.FromResult<IActionResult>(Problem(statusCode:400,title:"INVALID_ROLE_VERSION"));
  return Execute(()=>action(Convert.FromHexString(tag[1..^1])),ct);
 }
 private async Task<IActionResult> Execute(Func<Task<CustomRoleResult>> action,CancellationToken ct){Response.Headers.CacheControl="no-store";if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");try{var r=await action();if(r.Version!=null)Response.Headers.ETag="\""+Convert.ToHexString(r.Version)+"\"";return r.Status switch {201=>Created($"/api/roles/{r.Value!.RoleId}",r.Value),200=>Ok(r.Value),_=>Problem(statusCode:r.Status,title:r.Code)};}catch(ValidationException){return Problem(statusCode:400,title:"ROLE_VALIDATION_FAILED");}catch(Exception e)when(!ct.IsCancellationRequested&&e is DbException or DbUpdateException or SessionStoreUnavailableException){return Problem(statusCode:503,title:"ROLE_STORE_UNAVAILABLE");}}
}
