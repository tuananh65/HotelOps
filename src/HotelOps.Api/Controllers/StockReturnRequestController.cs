using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Inventory;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors, Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme), AutoValidateAntiforgeryToken]
[Route("api/return-requests"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StockReturnRequestController(StockReturnRequestWorkflow service, StockReturnQueue queue) : ControllerBase
{
    [HttpGet]
    public Task<object> List(CancellationToken ct, string mode = "mine", long after = 0) => queue.ReadAsync(mode, after, ct);

    [HttpGet("issues/{id:long}")]
    public Task<object> Issue(long id, CancellationToken ct) => service.IssueAsync(id, ct);

    [HttpGet("{id:long}")]
    public Task<object> Read(long id, CancellationToken ct) => service.ReadAsync(id, ct);

    [HttpPost]
    public async Task<IActionResult> Submit(InventoryDraft input, CancellationToken ct)
    {
        try { return Ok(await service.SubmitAsync(input, ct)); }
        catch (ValidationException e) { return Problem(statusCode: 400, title: "RETURN_INVALID", detail: e.Message); }
    }

    [HttpPost("{id:long}/withdraw")]
    public async Task<IActionResult> Withdraw(long id, InventoryCommand input, CancellationToken ct)
    {
        try { var result = await service.WithdrawAsync(id, input, ct); return result == null ? NotFound() : Ok(result); }
        catch (ValidationException e) { return Problem(statusCode: 409, title: "RETURN_CONFLICT", detail: e.Message); }
    }
}
