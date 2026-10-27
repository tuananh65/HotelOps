using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
[UserAccountActivationErrors] // Existing route-local413 translation; other failures retain middleware500/503.
public sealed class UserBranchAssignmentController(UserBranchAssignmentService assignments) : ControllerBase
{
    [HttpPost("{userId}/branches")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Add([FromRoute, Range(1, long.MaxValue)] long userId, [FromBody] JsonElement body, CancellationToken ct)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS is required");
        if (!HotelOpsCookieTicket.TryRead(User, out var reference, out var sessionId)) return Failure(401, "SESSION_INVALID");
        if (!Request.Headers.TryGetValue("If-Match", out var header)) return Failure(428, "IF_MATCH_REQUIRED");
        var tag = header.ToString();
        if (header.Count != 1 || tag.Length != 18 || tag[0] != '"' || tag[^1] != '"'
            || !tag.AsSpan(1, 16).ToString().All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F')) return Failure(400, "INVALID_IF_MATCH");
        if (!AddUserBranchRequest.TryParse(body, out var request)) return Failure(400, "VALIDATION_FAILED");
        var result = await assignments.AddAsync(reference!, sessionId, userId, request!.BranchId, Convert.FromHexString(tag[1..^1]), ct);
        switch (result.Status)
        {
            case AddBranchStatus.Success:
                Response.Headers.ETag = "\"" + Convert.ToHexString(result.Version!) + "\"";
                return StatusCode(201, result.Membership);
            case AddBranchStatus.SessionInvalid: return Failure(401, "SESSION_INVALID");
            case AddBranchStatus.Forbidden: return Failure(403, "BRANCH_ASSIGNMENT_NOT_ALLOWED");
            case AddBranchStatus.NotFound: return Failure(404, "USER_NOT_FOUND");
            case AddBranchStatus.Inactive: return Failure(409, "TARGET_NOT_ACTIVE");
            case AddBranchStatus.InvalidBranch: return Failure(400, "BRANCH_UNAVAILABLE");
            case AddBranchStatus.Stale: return Failure(412, "STALE_USER_VERSION");
            case AddBranchStatus.Duplicate: return Failure(409, "MEMBERSHIP_EXISTS");
            case AddBranchStatus.RoleConflict: return Failure(409, "EXISTING_ROLE_PATH_CONFLICT");
            case AddBranchStatus.OperationsManagerConflict: return Failure(409, "OPERATIONS_MANAGER_SCOPE_CONFLICT");
            default: throw new InvalidOperationException("Unknown branch assignment result.");
        }
    }
    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
