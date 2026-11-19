using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Controllers;

public sealed class RoomMoveRequest
{
    [Range(1, long.MaxValue)] public long TargetRoomId { get; init; }
    [Required, RegularExpression("^[A-F0-9]{64}$")] public string TargetVersion { get; init; } = "";
    [Required, StringLength(500, MinimumLength = 1)] public string Reason { get; init; } = "";
}

[ApiController, InsightErrors]
[Authorize(AuthenticationSchemes = HotelOpsCookieTicket.Scheme)]
[Route("api/branches/{branchId:long}/rooms/{roomId:long}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RoomDepartureController(RoomCheckInService service) : ControllerBase
{
    [HttpGet("check-out")]
    public async Task<IActionResult> Read([Range(1, long.MaxValue)] long branchId,
        [Range(1, long.MaxValue)] long roomId, CancellationToken ct)
        => Ok(await service.ReadCheckOutAsync(branchId, roomId, ct));

    [HttpGet("move-options")]
    public async Task<IActionResult> Options([Range(1, long.MaxValue)] long branchId,
        [Range(1, long.MaxValue)] long roomId, CancellationToken ct)
        => Ok(await service.ReadMoveOptionsAsync(branchId, roomId, ct));

    [HttpPost("check-out")]
    public async Task<IActionResult> CheckOut([Range(1, long.MaxValue)] long branchId,
        [Range(1, long.MaxValue)] long roomId, CancellationToken ct)
        => Version() is { } version ? Ok(await service.CheckOutAsync(branchId, roomId, version, ct)) : MissingVersion();

    [HttpPost("move")]
    public async Task<IActionResult> Move([Range(1, long.MaxValue)] long branchId,
        [Range(1, long.MaxValue)] long roomId, RoomMoveRequest request, CancellationToken ct)
        => Version() is { } version
            ? Ok(await service.MoveAsync(branchId, roomId, version, request.TargetRoomId, request.TargetVersion, request.Reason, ct))
            : MissingVersion();

    private string? Version()
    {
        var values = Request.Headers.IfMatch;
        return values.Count == 1 && values[0] is { Length: 66 } tag && tag[0] == '"' && tag[^1] == '"'
            && tag[1..^1].All(char.IsAsciiHexDigitUpper) ? tag[1..^1] : null;
    }

    private ObjectResult MissingVersion() => Problem(statusCode: 400, title: "INVALID_IF_MATCH",
        detail: "Cần tải lại thông tin phòng trước khi thực hiện.");
}
