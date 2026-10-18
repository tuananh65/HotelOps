namespace HotelOps.Api.DTOs.Assets;

public sealed class RejectAssetHandoverRequest : AssetActorRequest
{
    public string Reason { get; init; } = string.Empty;
}
