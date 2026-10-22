namespace HotelOps.Api.DTOs.Assets;

public sealed class TransferAssetLocationRequest : AssetActorRequest
{
    public string ExpectedLocationType { get; init; } = string.Empty;
    public long ExpectedLocationId { get; init; }
    public long? RoomId { get; init; }
    public long? AreaId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ConfirmationNote { get; init; }
}
