namespace HotelOps.Api.DTOs.HotelStructure;

public sealed record ChecklistCollectionRequest
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    public string? Status { get; init; }
    public long? RoomTypeId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ChecklistVersionSummaryDto(long Id, int Number, string Status,
    DateTime? EffectiveAt, long? PublishedBy, bool UsedByInspection);
public sealed record ChecklistListItemDto(long Id, long BranchId, string Code, string Name, bool Active,
    IReadOnlyList<long> RoomTypeIds, IReadOnlyList<ChecklistVersionSummaryDto> Versions);
public sealed record ChecklistCollectionDto(IReadOnlyList<ChecklistListItemDto> Items, int Total, int Page,
    int PageSize, long BranchId, bool? IsActive, string? Search, string? Status, long? RoomTypeId);