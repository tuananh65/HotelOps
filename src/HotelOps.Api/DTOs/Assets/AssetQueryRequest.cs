namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetQueryRequest : AssetPageRequest
{
    public long? BranchId { get; init; }
    public long? AssetTypeId { get; init; }
    public long? RoomId { get; init; }
    public long? AreaId { get; init; }
    public long? DepartmentId { get; init; }
    public string? ConditionStatus { get; init; }
    public string? LifecycleStatus { get; init; }
    public string? Search { get; init; }
}
