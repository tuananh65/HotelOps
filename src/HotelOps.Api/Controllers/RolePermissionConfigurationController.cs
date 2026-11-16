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
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Controllers;

[ApiController, Route("api/roles/{roleId:long}/permission-configuration")]
[Authorize(AuthenticationSchemes=HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class RolePermissionConfigurationController(HotelOpsDbContext db,ICurrentUserAccessor current,
    IPermissionService permissions,TimeProvider clock,IHttpContextAccessor http):ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Read(long roleId,CancellationToken ct)=>Execute(roleId,null,null,ct);

    [HttpPut,ValidateAntiForgeryToken,RequestSizeLimit(131072)]
    public Task<IActionResult> Write(long roleId,RolePermissionConfigurationRequest request,CancellationToken ct)
    {
        if(!Request.Headers.TryGetValue("If-Match",out var header))return Task.FromResult<IActionResult>(Problem(statusCode:428,title:"ROLE_VERSION_REQUIRED"));
        var tag=header.ToString();
        if(header.Count!=1||tag.Length!=18||tag[0]!='"'||tag[^1]!='"'||!tag.AsSpan(1,16).ToString().All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            return Task.FromResult<IActionResult>(Problem(statusCode:400,title:"INVALID_ROLE_VERSION"));
        return Execute(roleId,request,Convert.FromHexString(tag[1..^1]),ct);
    }

    private async Task<IActionResult> Execute(long id,RolePermissionConfigurationRequest? request,byte[]? version,CancellationToken ct)
    {
        if(!Request.IsHttps)return Problem(statusCode:400,title:"HTTPS_REQUIRED");
        try
        {
            var service=new CustomRoleService(db,current,permissions,clock,http);
            var result=await service.ConfigurePermissionsAsync(id,request,version,ct);
            if(result.Status!=200)return Problem(statusCode:result.Status,title:result.Code);
            Response.Headers.ETag="\""+Convert.ToHexString(result.Version!)+"\"";
            return Ok(result.Value);
        }
        catch(ValidationException){return Problem(statusCode:400,title:"INVALID_PERMISSION_CONFIGURATION");}
        catch(Exception e)when(!ct.IsCancellationRequested&&e is DbException or DbUpdateException or SessionStoreUnavailableException)
        {return Problem(statusCode:503,title:"ROLE_PERMISSION_STORE_UNAVAILABLE");}
    }
}
