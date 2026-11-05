using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/branches/{branchId}/structure")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BranchStructureController(BranchStructureService structures) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromRoute, Range(1, long.MaxValue)] long branchId,
        [FromQuery] BranchStructureQuery query, CancellationToken ct)
    {
        if (!HotelOpsCookieTicket.TryRead(User, out var ticket, out _)) return Error(401, "SESSION_INVALID");
        try
        {
            var result = await structures.ReadAsync(ticket!, branchId, query, ct);
            return result.Status switch
            {
                BranchStructureStatus.Success => Ok(result.Structure),
                BranchStructureStatus.SessionInvalid => Error(401, "SESSION_INVALID"),
                BranchStructureStatus.Forbidden => Error(403, "PERMISSION_DENIED"),
                BranchStructureStatus.NotFound => Error(404, "STRUCTURE_NODE_NOT_FOUND"),
                BranchStructureStatus.Unavailable => Error(503, "STRUCTURE_READ_UNAVAILABLE"),
                _ => throw new InvalidOperationException("Unknown branch structure status.")
            };
        }
        catch (ValidationException error) { return Problem(statusCode: 400, title: "VALIDATION_FAILED", detail: error.Message); }
    }

    private ObjectResult Error(int status, string code) => Problem(statusCode: status, title: code,
        extensions: new Dictionary<string, object?> { ["code"] = code });
}
