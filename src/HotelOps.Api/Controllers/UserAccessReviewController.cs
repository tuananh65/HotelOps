using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[TypeFilter(typeof(UserDirectoryReadFilter))]
public sealed class UserAccessReviewController(UserAccessReviewService reviews) : ControllerBase
{
    [HttpGet("{userId}/effective-access")]
    [UserAccessReview]
    public async Task<IActionResult> Get([FromRoute, Range(1, long.MaxValue)] long userId,
        [FromQuery] UserAccessReviewQuery query, CancellationToken ct)
    {
        var result = await reviews.ReadAsync(UserAccessReviewAttribute.Get(HttpContext), query, ct);
        return result is null ? Problem(statusCode: 404, title: "User account not found") : Ok(result);
    }
}
