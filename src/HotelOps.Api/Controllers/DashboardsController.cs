using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InsightErrors]
[Route("api/dashboards")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DashboardsController(DashboardService service, InsightAccess access) : ControllerBase
{
    [HttpGet("operations")]
    public Task<DashboardResult> Operations([FromQuery] InsightQuery query, CancellationToken ct) => service.ReadAsync(query, ct);

    [HttpGet("options")]
    public Task<InsightOptions> Options(CancellationToken ct) => access.OptionsAsync("DASHBOARD_VIEW", ct);
}
