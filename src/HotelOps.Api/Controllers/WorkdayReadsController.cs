using HotelOps.Api.Security;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

// Read-only activation: WorkdaysController remains NonController so its writes
// cannot be exposed by this routing change. Authorization stays in the service.
[ApiController]
[Route("api/branches/{branchId:long}/workdays")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkdayReadsController(WorkdayService service) : ControllerBase
{
    [HttpGet("own-shifts")]
    public Task<IActionResult> OwnShifts(long branchId, CancellationToken ct)
        => Execute(() => service.OwnShiftsAsync(branchId, ct));

    [HttpGet("current")]
    public Task<IActionResult> Current(long branchId, CancellationToken ct)
        => Execute(() => service.CurrentAsync(branchId, ct));

    private async Task<IActionResult> Execute(Func<Task<WorkdayResult>> action)
    {
        if (!Request.IsHttps) return Problem(statusCode: 400, title: "HTTPS_REQUIRED");
        var result = await action();
        if (result.Status >= 400) return Problem(statusCode: result.Status, title: result.Code);
        if (result.Value is OwnWorkingDaySnapshot snapshot)
            Response.Headers.ETag = '"' + snapshot.RowVersion + '"';
        return result.Status == 204 ? NoContent() : StatusCode(result.Status, result.Value);
    }
}
