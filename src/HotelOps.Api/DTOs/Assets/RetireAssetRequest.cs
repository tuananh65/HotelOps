namespace HotelOps.Api.DTOs.Assets;

public sealed class RetireAssetRequest : AssetActorRequest
{
    public string Reason { get; init; } = string.Empty;
}
