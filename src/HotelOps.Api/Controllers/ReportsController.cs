using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InsightErrors]
[Route("api/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(ReportReadService service, InsightAccess access) : ControllerBase
{
    [HttpGet("options")]
    public Task<InsightOptions> Options(CancellationToken ct) => access.OptionsAsync("REPORT_VIEW", ct);

    [HttpGet("{key}")]
    public Task<ReportResult> Preview(string key, [FromQuery] ReportQuery query, CancellationToken ct) => service.ReadAsync(key, query, ct);

    [HttpPost("snapshots/{snapshotId}/export")]
    public async Task<IActionResult> Export(string snapshotId, ExportRequest request, CancellationToken ct)
    {
        var file = await service.ExportAsync(snapshotId, request, ct);
        return File(file.Content, file.ContentType, file.Name);
    }
}
