namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetLocationStatisticsResponse
{
    public DateTime SnapshotAt { get; init; }
    public IReadOnlyList<AssetLocationStatisticsDto> Items { get; init; } = [];
}

public sealed class AssetLocationStatisticsDto
{
    public long BranchId { get; init; }
    // ROOM, AREA, UNASSIGNED or CORRUPT; never infer one location for corrupt rows.
    public string LocationType { get; init; } = string.Empty;
    public long? LocationId { get; init; }
    public string? DisplayName { get; init; }
    public int TotalCount { get; init; }
    // Only IN_USE/UNDER_REPAIR are operational. Other raw values remain distinct.
    public int OperationalCount { get; init; }
    public int RetiredCount { get; init; }
    public IReadOnlyList<AssetLifecycleCountDto> LifecycleCounts { get; init; } = [];
}

public sealed class AssetLifecycleCountDto
{
    public string LifecycleStatus { get; init; } = string.Empty;
    public int Count { get; init; }
}
