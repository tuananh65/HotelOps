using HotelOps.Api.DTOs.Insights;
using HotelOps.Api.Services.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, Authorize, InsightErrors]
[Route("api/audit-logs")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuditLogsController(AuditReadService service, InsightAccess access) : ControllerBase
{
    [HttpGet]
    public Task<AuditPage> List([FromQuery] AuditQuery query, CancellationToken ct) => service.ListAsync(query, ct);

    [HttpGet("options")]
    public Task<InsightOptions> Options(CancellationToken ct) => access.OptionsAsync("AUDIT_VIEW", ct);

    [HttpGet("{id:long:min(1)}")]
    public Task<AuditDetail> Detail(long id, CancellationToken ct) => service.DetailAsync(id, ct);
}
