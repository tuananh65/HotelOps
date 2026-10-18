using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Services.Assets;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController]
[Route("api/assets")]
public sealed class AssetsController(IAssetService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AssetPagedResponse<AssetListDto>>> GetAssets([FromQuery] AssetQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetAssetsAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AssetDetailDto>> GetAsset(long id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetAssetAsync(id, cancellationToken);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpGet("/api/asset-types")]
    public async Task<ActionResult<IReadOnlyList<AssetTypeLookupDto>>> GetAssetTypes(CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetAssetTypesAsync(cancellationToken);
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpGet("{id:long}/condition-history")]
    public async Task<ActionResult<AssetPagedResponse<AssetConditionHistoryDto>>> GetConditionHistory(long id, [FromQuery] AssetPageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetConditionHistoryAsync(id, request, cancellationToken);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpGet("{id:long}/location-history")]
    public async Task<ActionResult<AssetPagedResponse<AssetLocationHistoryDto>>> GetLocationHistory(long id, [FromQuery] AssetPageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetLocationHistoryAsync(id, request, cancellationToken);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpPost]
    public async Task<ActionResult<AssetDetailDto>> CreateAsset(CreateAssetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreateAssetAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetAsset), new { id = result.AssetId }, result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AssetDetailDto>> UpdateAsset(long id, UpdateAssetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.UpdateAssetAsync(id, request, cancellationToken);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpPost("{id:long}/location")]
    public async Task<ActionResult<AssetDetailDto>> AssignLocation(long id, AssignAssetLocationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.AssignLocationAsync(id, request, cancellationToken);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpPost("{id:long}/location-transfers")]
    public async Task<ActionResult<AssetDetailDto>> TransferLocation(long id, TransferAssetLocationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.TransferLocationAsync(id, request, cancellationToken);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

    [HttpPost("{id:long}/condition")]
    public async Task<ActionResult<AssetDetailDto>> ChangeCondition(long id, ChangeAssetConditionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ChangeConditionAsync(id, request, cancellationToken);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (ValidationException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Asset validation failed", detail: exception.Message);
        }
    }

}
