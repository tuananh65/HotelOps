namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetLocationHistoryDto
{
    public long LocationHistoryId { get; init; }
    public long AssetId { get; init; }
    public string? FromLocationType { get; init; }
    public long? FromLocationId { get; init; }
    public string ToLocationType { get; init; } = string.Empty;
    public long ToLocationId { get; init; }
    public DateTime EffectiveAt { get; init; }
    public long MovedBy { get; init; }
    public string? Reason { get; init; }
}
