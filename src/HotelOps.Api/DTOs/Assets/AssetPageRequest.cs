namespace HotelOps.Api.DTOs.Assets;

public class AssetPageRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
