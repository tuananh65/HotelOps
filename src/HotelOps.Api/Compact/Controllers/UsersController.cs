using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HotelOps.Api.Compact.Controllers;
[ApiController,Authorize,Route("api/v2/users")]
public sealed class UsersController(UserService service,AccountManagementService management):ControllerBase
{
    [HttpGet("roles/{id:long}/permissions")]public Task<object> RolePermissions(long id,CancellationToken ct)=>service.RolePermissions(id,ct);
    [HttpGet]public Task<object> List(CancellationToken ct)=>service.List(ct);
    [HttpGet("{id:long}")]public Task<object> Detail(long id,CancellationToken ct)=>management.Detail(id,ct);
    [HttpPut("{id:long}")]public Task<object> Update(long id,UpdateAccountRequest request,CancellationToken ct)=>management.Update(id,request,ct);
    [HttpPost("{id:long}/reset-password"),EnableRateLimiting("account-reset")]
    public Task<object> ResetPassword(long id,ResetAccountPasswordRequest request,CancellationToken ct)=>management.ResetPassword(id,request,ct);
    [HttpPost]public Task<object> Create(CreateUserRequest request,CancellationToken ct)=>service.Create(request,ct);
    [HttpPost("{id:long}/{command:regex(^(activate|deactivate)$)}")]public Task<object> Status(long id,string command,ActionRequest request,CancellationToken ct)=>service.Status(id,command,request,ct);
    [HttpPost("{id:long}/memberships")]public Task<object> Membership(long id,MembershipRequest request,CancellationToken ct)=>service.Membership(id,request,ct);
    [HttpPost("memberships/{id:long}/revoke")]public Task<object> RevokeMembership(long id,VersionRequest request,CancellationToken ct)=>service.RevokeMembership(id,request,ct);
    [HttpPost("{id:long}/roles")]public Task<object> Role(long id,UserRoleRequest request,CancellationToken ct)=>service.AssignRole(id,request,ct);
    [HttpPost("role-assignments/{id:long}/revoke")]public Task<object> RevokeRole(long id,VersionRequest request,CancellationToken ct)=>service.RevokeRole(id,request,ct);
    [HttpPut("roles/{id:long}/permissions")]public Task<object> Permissions(long id,IReadOnlyList<GrantRequest> request,CancellationToken ct)=>service.ConfigureRole(id,request,ct);
}
