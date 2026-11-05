using HotelOps.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/auth/csrf")]
public sealed class CsrfController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<CsrfBootstrapResponse> Get() => HotelOpsAntiforgery.Issue(HttpContext, antiforgery);
}
