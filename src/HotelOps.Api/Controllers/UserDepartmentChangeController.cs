using HotelOps.Api.DTOs.Users;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;

[NonController] // Source only; no DI/public writer activation before independent trial acceptance.
[ApiController, Route("api/users"), Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UserDepartmentChangeController(UserDepartmentChangeService service) : ControllerBase
{
    [HttpPut("{userId:long}/department"), ValidateAntiForgeryToken, RequestSizeLimit(1024)]
    public async Task<IActionResult> Change(long userId, ChangeDepartmentRequest input, CancellationToken ct)
    {
        if (!Request.IsHttps) return Failure(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var session)) return Failure(401, "SESSION_INVALID");
        if (userId <= 0 || input.DepartmentId <= 0) return Failure(400, "VALIDATION_FAILED");
        if (!Request.Headers.ContainsKey("If-Match")) return Failure(428, "IF_MATCH_REQUIRED");
        if (Request.Headers.IfMatch.Count != 1 || !RevokeRoleVersion.TryParse(Request.Headers.IfMatch.ToString(), out var version)) return Failure(400, "INVALID_IF_MATCH");
        var r = await service.ChangeAsync(ticket!, session, userId, input, version, ct);
        if (r.Status == DepartmentChangeStatus.Success) { Response.Headers.ETag = '"' + Convert.ToHexString(r.Version!) + '"'; return Ok(r.Value); }
        return r.Status switch
        {
            DepartmentChangeStatus.SessionInvalid => Failure(401, "SESSION_INVALID"),
            DepartmentChangeStatus.Forbidden => Failure(403, "DEPARTMENT_CHANGE_NOT_ALLOWED"),
            DepartmentChangeStatus.NotFound => Failure(404, "USER_NOT_FOUND"),
            DepartmentChangeStatus.Stale => Failure(412, "STALE_USER_VERSION"),
            DepartmentChangeStatus.RoleConflict => Failure(409, "ROLE_SCOPE_CONFLICT"),
            DepartmentChangeStatus.NoChange => Failure(409, "DEPARTMENT_UNCHANGED"),
            DepartmentChangeStatus.InvalidScope => Failure(409, "INVALID_DEPARTMENT_SCOPE"),
            DepartmentChangeStatus.OpenWork => Failure(409, "OPEN_RESPONSIBILITIES"),
            DepartmentChangeStatus.LastAdministrator => Failure(409, "LAST_DURABLE_ADMIN"),
            _ => Failure(503, "DEPARTMENT_CHANGE_ACCEPTANCE_REQUIRED")
        };
    }
    [HttpGet("{userId:long}/department")]
    public async Task<IActionResult> Read(long userId, CancellationToken ct)
    {
        if (!Request.IsHttps) return Failure(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var session)) return Failure(401, "SESSION_INVALID");
        if (userId <= 0) return Failure(400, "VALIDATION_FAILED");
        var r = await service.ReadAsync(ticket!, session, userId, ct);
        if (r.Status == DepartmentChangeStatus.Success) { Response.Headers.ETag = '"' + Convert.ToHexString(r.Version!) + '"'; return Ok(r.Value); }
        return Failure(r.Status == DepartmentChangeStatus.SessionInvalid ? 401 : r.Status == DepartmentChangeStatus.Forbidden ? 403 : r.Status == DepartmentChangeStatus.NotFound ? 404 : r.Status == DepartmentChangeStatus.CoverageRequired ? 503 : 409, "DEPARTMENT_REVIEW_UNAVAILABLE");
    }
    private ObjectResult Failure(int status, string code) => Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code });
}