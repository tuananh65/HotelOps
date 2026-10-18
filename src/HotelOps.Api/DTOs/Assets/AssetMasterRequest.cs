namespace HotelOps.Api.DTOs.Assets;

public abstract class AssetMasterRequest : AssetActorRequest
{
    public string AssetCode { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public long AssetTypeId { get; init; }
    public string? SerialNumber { get; init; }
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public DateOnly? WarrantyExpiryDate { get; init; }
    public string? Description { get; init; }
}
