using HotelOps.Api.Security;
using HotelOps.Api.Services.WorkTime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

// Read routes are separate from scheduled workday writes. Authorization stays in the service.
[ApiController]
[Route("api/branches/{branchId:long}/workdays")]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkdayReadsController(WorkdayService service) : ControllerBase
{
    [HttpGet("/api/workdays/workspace-options")]
    public Task<IActionResult> WorkspaceOptions(CancellationToken ct)
        => Execute(() => service.WorkspaceOptionsAsync(ct));

    [HttpGet("/api/workdays/branch-options")]
    public Task<IActionResult> BranchOptions(CancellationToken ct)
        => Execute(() => service.BranchOptionsAsync(ct));

    [HttpGet("own-departments")]
    public Task<IActionResult> OwnDepartments(long branchId, CancellationToken ct)
        => Execute(() => service.OwnDepartmentsAsync(branchId, ct));

    [HttpGet("{workdayId:long}/unscheduled-summary")]
    public Task<IActionResult> UnscheduledSummary(long branchId, long workdayId, CancellationToken ct)
        => Execute(() => service.UnscheduledSummaryAsync(branchId, workdayId, ct));

    [HttpGet("own-shifts/{assignmentId:long}")]
    public Task<IActionResult> OwnShiftAssignment(long branchId,long assignmentId,CancellationToken ct)
        => Execute(()=>service.OwnShiftAssignmentAsync(branchId,assignmentId,ct));

    [HttpGet("own-shifts")]
    public Task<IActionResult> OwnShifts(long branchId, CancellationToken ct)
        => Execute(() => service.OwnShiftsAsync(branchId, ct));

    [HttpGet("current")]
    public Task<IActionResult> Current(long branchId, CancellationToken ct)
        => Execute(() => service.CurrentAsync(branchId, ct));

    [HttpGet("own-time-log")]
    public Task<IActionResult> OwnTimeLog(long branchId,[FromQuery] DateOnly date,CancellationToken ct,
        [FromQuery] int page=1,[FromQuery] int pageSize=50)
        => Execute(() => service.OwnTimeLogAsync(branchId,date,page,pageSize,ct));

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
