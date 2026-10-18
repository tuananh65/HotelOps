namespace HotelOps.Api.DTOs.Assets;

public class AssetListDto
{
    public long AssetId { get; init; }
    public string AssetCode { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public long AssetTypeId { get; init; }
    public string AssetTypeCode { get; init; } = string.Empty;
    public string AssetTypeName { get; init; } = string.Empty;
    public long BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public string ConditionStatus { get; init; } = string.Empty;
    public string LifecycleStatus { get; init; } = string.Empty;
    public long? CurrentRoomId { get; init; }
    public string? RoomNumber { get; init; }
    public long? CurrentAreaId { get; init; }
    public string? AreaName { get; init; }
    public long? CurrentDepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public long? CustodianUserId { get; init; }
    public string? CustodianName { get; init; }
}
