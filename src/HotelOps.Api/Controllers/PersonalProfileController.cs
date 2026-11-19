using HotelOps.Api.Security;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/auth/profile")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PersonalProfileController(PersonalProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PersonalProfileDto>> Read(CancellationToken ct) => Ok(await service.ReadAsync(ct));
}
