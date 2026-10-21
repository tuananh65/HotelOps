using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Replenishments;
using HotelOps.Api.Services.Replenishments;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/replenishments")]
public sealed class ReplenishmentsController(ReplenishmentService service) : ControllerBase
{
    private async Task<IActionResult> Execute<T>(Func<Task<T>> action, Func<T, string>? location = null)
    {
        try { var result = await action(); return result is null ? NotFound() : location is null ? Ok(result) : Created(location(result), result); }
        catch (ValidationException e) { return Problem(statusCode: 400, title: "Replenishment validation failed", detail: e.Message); }
    }
    [HttpGet] public Task<IActionResult> List([FromQuery] ReplenishmentPage page, CancellationToken ct) => Execute(() => service.List(page, ct));
    [HttpGet("{id:long}")] public Task<IActionResult> Get(long id, CancellationToken ct) => Execute(() => service.Get(id, ct));
    [HttpGet("{id:long}/history")] public Task<IActionResult> History(long id, CancellationToken ct) => Execute(() => service.History(id, ct));
    [HttpGet("{id:long}/receipts")] public Task<IActionResult> Receipts(long id, CancellationToken ct) => Execute(() => service.Receipts(id, ct));
    [HttpPost] public Task<IActionResult> Create(ReplenishmentCreate input, CancellationToken ct) => Execute(() => service.Create(input, ct), x => $"/api/replenishments/{x.ReplenishmentId}");
    [HttpPost("{id:long}/close")] public Task<IActionResult> Close(long id, ReplenishmentClose input, CancellationToken ct) => Execute(() => service.Close(id, input, ct));
    [HttpPost("{id:long}/receipts/link")] public Task<IActionResult> Link(long id, ReplenishmentLink input, CancellationToken ct) => Execute(() => service.Receive(id, input, ct));
    [HttpPost("{id:long}/receipts/post")] public Task<IActionResult> Post(long id, ReplenishmentPost input, CancellationToken ct) => Execute(() => service.Receive(id, new(input.ActorUserId, input.ExpectedRowVersion, input.Lines), ct, input));
    [HttpGet("/api/replenishment-shortages/low-stock")] public Task<IActionResult> Low([FromQuery] ReplenishmentPage page, CancellationToken ct) => Execute(() => service.LowStock(page, ct));
    [HttpGet("/api/replenishment-shortages/material-requests")] public Task<IActionResult> Material([FromQuery] ReplenishmentPage page, CancellationToken ct) => Execute(() => service.MaterialShortages(page, ct));
}
