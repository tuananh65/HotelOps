namespace HotelOps.Api.DTOs.Assets;

public sealed class AssignAssetLocationRequest : AssetActorRequest
{
    public long? RoomId { get; init; }
    public long? AreaId { get; init; }
    public string? Reason { get; init; }
}
