using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

[ApiController, InsightErrors]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/branches/{branchId:long}/rooms/{roomId:long}/check-in")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RoomCheckInController(RoomCheckInService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Read([Range(1, long.MaxValue)] long branchId,
        [Range(1, long.MaxValue)] long roomId, CancellationToken ct)
        => Ok(await service.ReadAsync(branchId, roomId, ct));

    [HttpPost]
    public async Task<IActionResult> CheckIn([Range(1, long.MaxValue)] long branchId,
        [Range(1, long.MaxValue)] long roomId, CancellationToken ct)
    {
        var values = Request.Headers.IfMatch;
        if (values.Count != 1 || values[0] is not { Length: 66 } tag || tag[0] != '"' || tag[^1] != '"'
            || !tag[1..^1].All(c => char.IsAsciiHexDigitUpper(c)))
            return Problem(statusCode: 400, title: "INVALID_IF_MATCH", detail: "Cần tải thông tin phòng trước khi nhận khách.");
        return Ok(await service.CheckInAsync(branchId, roomId, tag[1..^1], ct));
    }
}
