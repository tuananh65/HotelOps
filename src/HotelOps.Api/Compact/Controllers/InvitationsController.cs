using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace HotelOps.Api.Compact.Controllers;
[ApiController,Route("api/v2/invitations")]
public sealed class InvitationsController(AccountInvitationService service,IInvitationEmail email):ControllerBase
{
    [Authorize,HttpGet("users/{id:long}")]public async Task<object> Status(long id,CancellationToken ct)=>new{items=await service.Status(id,ct),configurationError=email.ConfigurationError};
    [Authorize,HttpPost("users/{id:long}/resend"),EnableRateLimiting("account-reset")]public Task<object> Resend(long id,ResendInvitationRequest request,CancellationToken ct)=>service.Resend(id,request,ct);
    [AllowAnonymous,HttpPost("setup-password"),EnableRateLimiting("account-reset")]public Task<object> Setup(SetupAccountPasswordRequest request,CancellationToken ct)=>service.Setup(request,ct);
    [AllowAnonymous,HttpPost("forgot-password"),EnableRateLimiting("account-reset")]
    [ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
    public Task<object> Forgot(ForgotAccountPasswordRequest request,CancellationToken ct)=>service.Forgot(request,ct);
}
