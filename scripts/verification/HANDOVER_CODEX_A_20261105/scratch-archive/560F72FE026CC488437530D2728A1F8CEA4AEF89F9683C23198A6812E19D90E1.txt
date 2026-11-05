using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = PermissionPolicy.Prefix + "Global:USER_VIEW")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(IUserDirectoryService users) : ControllerBase
{
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserAccountDetailDto>> Detail([FromRoute, Range(1, long.MaxValue)] long userId,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(userId, cancellationToken);
        return user is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "User account not found")
            : Ok(user);
    }

    [HttpGet]
    public async Task<ActionResult<UserDirectoryResponse>> List([FromQuery] UserDirectoryQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await users.ListAsync(query, cancellationToken));
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "User directory validation failed", detail: exception.Message);
        }
    }
}
