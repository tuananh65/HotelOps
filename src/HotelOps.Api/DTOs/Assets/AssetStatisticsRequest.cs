namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetStatisticsRequest
{
    public long? BranchId { get; init; }
    public long? AssetTypeId { get; init; }
    public long? DepartmentId { get; init; }
    public string? ConditionStatus { get; init; }
    public string? LifecycleStatus { get; init; }
}
