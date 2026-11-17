using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), InsightErrors]
[Route("api/auth/navigation")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HomeNavigationController(HomeNavigationService navigation) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct)
    {
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out _)) return Unauthorized();
        var result = await navigation.ReadAsync(ticket!, ct);
        return result.Status switch
        {
            UserDirectoryReadStatus.Success => Ok(new { paths = result.Value }),
            UserDirectoryReadStatus.SessionInvalid => Unauthorized(),
            UserDirectoryReadStatus.Forbidden => Forbid(),
            _ => StatusCode(503)
        };
    }
}
