using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

/// <summary>UC012 metadata only. Deactivation remains on the undiscovered administration controller.</summary>
[ApiController]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors]
[RequestSizeLimit(32 * 1024)]
public sealed class BranchMaintenanceController(BranchAdministrationService service) : ControllerBase
{
    [HttpPost("api/branches")]
    public Task<IActionResult> Create(CreateBranchRequest input, CancellationToken ct) =>
        Execute((ticket, sid) => service.CreateAsync(ticket, sid, input, ct), false);

    [HttpGet("api/branches/{id:long}/edit")]
    public Task<IActionResult> Read([Range(1, long.MaxValue)] long id, CancellationToken ct) =>
        Execute((ticket, sid) => service.ReadAsync(ticket, sid, id, ct), false);

    [HttpPut("api/branches/{id:long}")]
    public Task<IActionResult> Update([Range(1, long.MaxValue)] long id, UpdateBranchRequest input, CancellationToken ct) =>
        Execute((ticket, sid) => service.UpdateAsync(ticket, sid, id, Version(), input, ct), true);

    private byte[] Version()
    {
        var values = Request.Headers.IfMatch;
        if (values.Count != 1 || values[0] is not { Length: 14 } text || text[0] != '"' || text[^1] != '"')
            throw new ValidationException("INVALID_IF_MATCH");
        try
        {
            var bytes = Convert.FromBase64String(text[1..^1]);
            if (bytes.Length == 8 && Convert.ToBase64String(bytes) == text[1..^1]) return bytes;
        }
        catch (FormatException) { }
        throw new ValidationException("INVALID_IF_MATCH");
    }

    private async Task<IActionResult> Execute(
        Func<HotelOps.Api.Services.Authentication.SessionReference, long, Task<BranchAdministrationResult>> call,
        bool conditional)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sid)) return Problem(statusCode: 401, title: "SESSION_INVALID");
        if (conditional && !Request.Headers.ContainsKey("If-Match")) return Problem(statusCode: 428, title: "IF_MATCH_REQUIRED");
        try { return ResponseFor(await call(ticket!, sid)); }
        catch (ValidationException) { return Problem(statusCode: 400, title: "VALIDATION_FAILED"); }
    }

    private IActionResult ResponseFor(BranchAdministrationResult result)
    {
        if (result.Status is >= 200 and < 300)
        {
            if (result.ETag != null) Response.Headers.ETag = result.ETag;
            return StatusCode(result.Status, result.Value);
        }
        return Problem(statusCode: result.Status, title: result.Code);
    }
}
