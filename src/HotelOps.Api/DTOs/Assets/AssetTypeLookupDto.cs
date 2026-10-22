namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetTypeLookupDto
{
    public long AssetTypeId { get; init; }
    public string TypeCode { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string? Description { get; init; }
}
