using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Data;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Rbac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController]
[Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[TypeFilter(typeof(RoleCatalogReadFilter),Order=-3000)]
public sealed class RoleCatalogController(HotelOpsDbContext db,TimeProvider clock):ControllerBase
{
 private readonly RoleCatalogService catalog=new(db,clock);
 [HttpGet("api/roles")]
 public async Task<IActionResult> Roles(int page=1,int pageSize=25,string? search=null,bool? isActive=null,CancellationToken ct=default){try{return Ok(await catalog.RolesAsync(page,pageSize,search,isActive,ct));}catch(ValidationException){return RoleCatalogReadFilter.Error(400,"INVALID_CATALOG_QUERY");}}
 [HttpGet("api/roles/{roleId}")]
 public async Task<IActionResult> Detail([Range(1,long.MaxValue)]long roleId,CancellationToken ct){var d=await catalog.DetailAsync(roleId,ct);return d==null?RoleCatalogReadFilter.Error(404,"ROLE_NOT_FOUND"):Ok(d);}
 [HttpGet("api/roles/{roleId}/permissions")]
 public async Task<IActionResult> Grants([Range(1,long.MaxValue)]long roleId,CancellationToken ct){var d=await catalog.DetailAsync(roleId,ct);return d==null?RoleCatalogReadFilter.Error(404,"ROLE_NOT_FOUND"):Ok(new HotelOps.Api.DTOs.Rbac.RoleGrantsDto(roleId,d.Grants));}
 [HttpGet("api/permissions/catalog")]
 public async Task<IActionResult> Permissions(int page=1,int pageSize=25,CancellationToken ct=default){try{return Ok(await catalog.PermissionsAsync(page,pageSize,ct));}catch(ValidationException){return RoleCatalogReadFilter.Error(400,"INVALID_CATALOG_QUERY");}}
}
