using HotelOps.Api.DTOs.Notifications;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HotelOps.Api.Controllers;
[ApiController,Authorize,InsightErrors]
[Route("api/notifications")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class NotificationsController(NotificationCenterService service):ControllerBase
{
    [HttpGet] public Task<NotificationPage> List([FromQuery] NotificationQuery query,CancellationToken ct)=>service.ListAsync(query,ct);
    [HttpGet("{id:long}/source")] public Task<NotificationSource> Source(long id,CancellationToken ct)=>service.ResolveSourceAsync(id,ct);
    [HttpGet("{id:long}/asset-handover")]
    public async Task<ActionResult<AssetHandoverNoticeDto>> AssetHandover(long id,CancellationToken ct)
    {
        if(!Request.IsHttps)return BadRequest(new {code="HTTPS_REQUIRED"});
        return await service.ReadAssetHandoverAsync(id,ct);
    }
    [HttpGet("{id:long}/asset-condition")]
    public async Task<ActionResult<AssetConditionFollowupDto>> AssetCondition(long id,CancellationToken ct)
    {
        if(!Request.IsHttps)return BadRequest(new {code="HTTPS_REQUIRED"});
        return await service.ReadAssetConditionAsync(id,ct);
    }
    [HttpGet("{id:long}/cleaning-cancellation")] public Task<CleaningCancellationReceiptDto> CleaningCancellation(long id,CancellationToken ct)=>service.ReadCleaningCancellationAsync(id,ct);
    [HttpPost("{id:long}/read")] public async Task<IActionResult> Read(long id,CancellationToken ct){await service.MarkReadAsync(id,ct);return NoContent();}
    [HttpPost("read-all")] public async Task<IActionResult> ReadAll(CancellationToken ct){await service.MarkReadAsync(null,ct);return NoContent();}
    [HttpPost("read-selected")] public async Task<IActionResult> ReadSelected(ReadNotificationsRequest request,CancellationToken ct){await service.MarkSelectedReadAsync(request.Ids,ct);return NoContent();}
    [HttpPost("acknowledge")] public async Task<IActionResult> Acknowledge(ReadNotificationsRequest request,CancellationToken ct){await service.AcknowledgeAsync(request.Ids,ct);return NoContent();}
}
