using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.MaterialRequests;
using HotelOps.Api.Services.MaterialRequests;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/material-requests")]
public sealed class MaterialRequestsController(MaterialRequestService service) : ControllerBase
{
    private async Task<IActionResult> Execute<T>(Func<Task<T>> operation, Func<T, string>? location = null)
    {
        try { var result = await operation(); return result is null ? NotFound() : location is null ? Ok(result) : Created(location(result), result); }
        catch (ValidationException e) { return Problem(statusCode: 400, title: "Material Request validation failed", detail: e.Message); }
    }
    [HttpGet] public Task<IActionResult> List([FromQuery] MaterialPage page, CancellationToken ct) => Execute(() => service.List(page, ct));
    [HttpGet("{id:long}")] public Task<IActionResult> Get(long id, CancellationToken ct) => Execute(() => service.Get(id, ct));
    [HttpGet("{id:long}/history")] public Task<IActionResult> History(long id, CancellationToken ct) => Execute(() => service.History(id, ct));
    [HttpGet("{id:long}/availability")] public Task<IActionResult> Availability(long id, CancellationToken ct) => Execute(() => service.Availability(id, ct));
    [HttpGet("{id:long}/fulfillments")] public Task<IActionResult> Fulfillments(long id, CancellationToken ct) => Execute(() => service.Fulfillments(id, ct));
    [HttpGet("{id:long}/fulfillments/{fulfillmentId:long}")] public Task<IActionResult> Fulfillment(long id, long fulfillmentId, CancellationToken ct) => Execute(() => service.Fulfillment(id, fulfillmentId, ct));
    [HttpPost] public Task<IActionResult> Create(MaterialRequestWrite input, CancellationToken ct) => Execute(() => service.Save(null, input, ct), x => $"/api/material-requests/{x!.RequestId}");
    [HttpPut("{id:long}")] public Task<IActionResult> Edit(long id, MaterialRequestWrite input, CancellationToken ct) => Execute(() => service.Save(id, input, ct));
    [HttpPost("{id:long}/submit")] public Task<IActionResult> Submit(long id, MaterialCommand input, CancellationToken ct) => Execute(() => service.Command(id, "submit", input, ct));
    [HttpPost("{id:long}/request-information")] public Task<IActionResult> Information(long id, MaterialCommand input, CancellationToken ct) => Execute(() => service.Command(id, "request-information", input, ct));
    [HttpPost("{id:long}/approve")] public Task<IActionResult> Approve(long id, MaterialApproval input, CancellationToken ct) => Execute(() => service.Command(id, "approve", new(input.ActorUserId, input.ExpectedRowVersion, input.Reason), ct, input));
    [HttpPost("{id:long}/reject")] public Task<IActionResult> Reject(long id, MaterialCommand input, CancellationToken ct) => Execute(() => service.Command(id, "reject", input, ct));
    [HttpPost("{id:long}/cancel")] public Task<IActionResult> Cancel(long id, MaterialCommand input, CancellationToken ct) => Execute(() => service.Command(id, "cancel", input, ct));
    [HttpPost("{id:long}/close-remainder")] public Task<IActionResult> Close(long id, MaterialCommand input, CancellationToken ct) => Execute(() => service.Command(id, "close-remainder", input, ct));
    [HttpPost("{id:long}/fulfillments")] public Task<IActionResult> Issue(long id, MaterialIssue input, CancellationToken ct) => Execute(() => service.Issue(id, input, ct), x => $"/api/material-requests/{id}/fulfillments/{x!.FulfillmentId}");
}
