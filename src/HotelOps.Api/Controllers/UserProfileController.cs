using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserProfileController(UserProfileService profiles, UserProfileAccess access) : ControllerBase
{
    [HttpGet("{userId}/profile")]
    public async Task<ActionResult<UserProfileDto>> Read([FromRoute, Range(1, long.MaxValue)] long userId, CancellationToken ct)
        => Respond(await profiles.ReadAsync(userId, ct));

    [HttpPatch("{userId}")]
    public async Task<ActionResult<UserProfileDto>> Patch([FromRoute, Range(1, long.MaxValue)] long userId,
        [FromBody] JsonElement body, CancellationToken ct)
    {
        if (!await access.CanManageAsync(userId, ct)) return Forbid();
        if (!Request.Headers.TryGetValue("If-Match", out var header))
            return Problem(statusCode: 428, title: "If-Match is required");
        var tag = header.ToString();
        if (header.Count != 1 || tag.Length != 18 || tag[0] != '"' || tag[^1] != '"'
            || !tag.AsSpan(1, 16).ToString().All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'))
            return Problem(statusCode: 400, title: "One strong rowversion ETag is required");
        UserProfilePatch patch;
        try { patch = UserProfilePatch.Parse(body); }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "Profile validation failed", detail: error.Message); }
        return Respond(await profiles.UpdateAsync(userId, Convert.FromHexString(tag[1..^1]), patch, ct));
    }

    private ActionResult<UserProfileDto> Respond(UserProfileResult result)
    {
        switch (result.Status)
        {
            case UserProfileStatus.Success:
                Response.Headers.ETag = "\"" + Convert.ToHexString(result.Version!) + "\"";
                return Ok(result.Profile);
            case UserProfileStatus.Forbidden: return Forbid();
            case UserProfileStatus.NotFound: return Problem(statusCode: 404, title: "User account not found");
            case UserProfileStatus.Stale: return Problem(statusCode: 412, title: "Profile version changed; reload before updating");
            case UserProfileStatus.EmailInUse: return Problem(statusCode: 409, title: "Email is already in use");
            case UserProfileStatus.LastAdministrator: return Problem(statusCode: 409, title: "LAST_DURABLE_ADMIN");
            case UserProfileStatus.SerializationUnavailable: return Problem(statusCode: 503, title: "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE");
            default: throw new InvalidOperationException("Unknown profile outcome.");
        }
    }
}
