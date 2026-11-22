using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HotelOps.Api.Compact.Controllers;

[ApiController,Route("api/v2/auth"),Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AuthController(AuthService service):ControllerBase
{
    [HttpPost("login"),AllowAnonymous,EnableRateLimiting("login")]
    public Task<object> Login(LoginRequest request,CancellationToken ct)=>service.Login(request,ct);
    [HttpGet("me")]public Task<object> Me(CancellationToken ct)=>service.Me(ct);
    [HttpPost("logout")]public Task<object> Logout(CancellationToken ct)=>service.Logout(ct);
    [HttpPost("password")]public Task<object> Password(PasswordRequest request,CancellationToken ct)=>service.ChangePassword(request,ct);
}
