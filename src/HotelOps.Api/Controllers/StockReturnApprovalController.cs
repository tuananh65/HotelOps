using HotelOps.Api.Security;
using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), AutoValidateAntiforgeryToken]
[Route("api/return-approvals")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StockReturnApprovalController(StockReturnApprovalWorkflow service) : ControllerBase
{
    [HttpGet("{id:long}")]
    public Task<object> Read(long id, CancellationToken ct) => service.ReadAsync(id, ct);

    [HttpPost("{id:long}")]
    public async Task<IActionResult> Approve(long id, StockReturnApproveInput input, CancellationToken ct)
    {
        try { return Ok(await service.ApproveAsync(id, input, ct)); }
        catch (ValidationException e) { return Problem(statusCode: 409, title: "RETURN_CONFLICT", detail: e.Message); }
    }
}
