namespace HotelOps.Api.DTOs.Maintenance;

public sealed class MaintenancePagedResponse
{
    public IReadOnlyList<MaintenanceWorkListDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
