using HotelOps.Api.Security;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors, AutoValidateAntiforgeryToken]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/asset-registration")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AssetRegistrationController(AssetRegistrationCatalog catalog, AssetRegistrationWriter writer,
    IConfiguration configuration, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("catalog")]
    public Task<object> Catalog(CancellationToken ct) => catalog.ReadAsync(ct);

    [HttpPost, RequestSizeLimit(72 * 1024 * 1024)]
    public async Task<object> Create(AssetRegistrationInput input, CancellationToken ct)
    {
        try { return await writer.CreateAsync(input, configuration["Evidence:PrivateRoot"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "evidence"), ct); }
        catch (System.ComponentModel.DataAnnotations.ValidationException e)
        { throw new HotelOps.Api.Services.Insights.InsightException(400, "MSG23", e.Message); }
    }
}
