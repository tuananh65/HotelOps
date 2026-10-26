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
