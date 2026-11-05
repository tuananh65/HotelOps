using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/branches/{branchId}")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[UserAccountActivationErrors] // Reuse the existing route-local body-limit translation.
public sealed class HotelStructureCreationController(HotelStructureCreationService structures) : ControllerBase
{
    [HttpPost("floors")]
    [RequestSizeLimit(32 * 1024)]
    public Task<IActionResult> CreateFloor([FromRoute, Range(1, long.MaxValue)] long branchId,
        [FromBody] CreateFloorRequest request, CancellationToken ct) => Create((ticket, id) =>
            structures.CreateFloorAsync(ticket, id, branchId, request, ct));

    [HttpPost("areas")]
    [RequestSizeLimit(32 * 1024)]
    public Task<IActionResult> CreateArea([FromRoute, Range(1, long.MaxValue)] long branchId,
        [FromBody] CreateAreaRequest request, CancellationToken ct) => Create((ticket, id) =>
            structures.CreateAreaAsync(ticket, id, branchId, request, ct));

    private async Task<IActionResult> Create(Func<SessionReference, long, Task<StructureCreationResult>> create)
    {
        if (!Request.IsHttps) return Error(400, "HTTPS_REQUIRED");
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out var sessionId)) return Error(401, "SESSION_INVALID");
        try
        {
            var result = await create(ticket!, sessionId);
            return result.Status switch
            {
                StructureCreationStatus.Success => StatusCode(201, result.Value),
                StructureCreationStatus.SessionInvalid => Error(401, "SESSION_INVALID"),
                StructureCreationStatus.Forbidden => Error(403, "PERMISSION_DENIED"),
                StructureCreationStatus.InvalidBranch => Error(400, "BRANCH_UNAVAILABLE"),
                StructureCreationStatus.InvalidFloor => Error(400, "PARENT_FLOOR_UNAVAILABLE"),
                StructureCreationStatus.DuplicateCode => Error(409, "STRUCTURE_CODE_EXISTS"),
                StructureCreationStatus.Unavailable => Error(503, "STRUCTURE_WRITE_UNAVAILABLE"),
                _ => throw new InvalidOperationException("Unknown structure creation status.")
            };
        }
        catch (ValidationException error)
        { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }

    private ObjectResult Error(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
