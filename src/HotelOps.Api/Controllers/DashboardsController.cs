using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InsightErrors]
[Route("api/dashboards")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DashboardsController(DashboardService service, RoomDashboardService rooms, DashboardDrillDownService drillDown) : ControllerBase
{
    [HttpGet("sources/{source}")]
    public Task<DashboardDrillResult> Source(string source, [FromQuery] DashboardDrillQuery query, CancellationToken ct)
        => drillDown.ReadAsync(source, query, ct);
    [HttpGet("rooms")]
    public Task<DashboardResult> Rooms([FromQuery] InsightQuery query, CancellationToken ct) => rooms.ReadAsync(query, ct);

    [HttpGet("rooms/options")]
    public Task<InsightOptions> RoomOptions(CancellationToken ct) => rooms.OptionsAsync(ct);

    [HttpGet("operations")]
    public Task<DashboardResult> Operations([FromQuery] InsightQuery query, CancellationToken ct) => service.ReadAsync(query, ct);

    [HttpGet("options")]
    public Task<InsightOptions> Options(CancellationToken ct) => rooms.OptionsAsync(ct);
}
