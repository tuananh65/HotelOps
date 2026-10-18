namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetDetailDto : AssetListDto
{
    public string? SerialNumber { get; init; }
    public string? Brand { get; init; }
    public string? Model { get; init; }
    public DateOnly? PurchaseDate { get; init; }
    public DateOnly? WarrantyExpiryDate { get; init; }
    public decimal? AcquisitionCost { get; init; }
    public string? Description { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
