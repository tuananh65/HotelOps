using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/asset-profiles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssetProfilesController(AssetProfileReader reader, IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("catalog")]
    public Task<object> Catalog(long? branch, CancellationToken ct) => reader.CatalogAsync(branch, ct);
    [HttpGet]
    public Task<object> List([FromQuery] AssetQueryRequest filter, CancellationToken ct) => reader.ListAsync(filter, ct);
    [HttpGet("{id:long}")]
    public Task<object> Read(long id, CancellationToken ct, int page = 1) => reader.ReadAsync(id, page, ct);
    [HttpGet("{id:long}/files/{file:long}")]
    public async Task<IActionResult> Download(long id, long file, CancellationToken ct)
    {
        var result = await reader.DownloadAsync(id, file, configuration["Evidence:PrivateRoot"]
            ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence"), ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "sandbox";
        return File(result.Bytes, result.MimeType, result.Name);
    }
}
