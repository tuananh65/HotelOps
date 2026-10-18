namespace HotelOps.Api.DTOs.Assets;

public sealed class CreateAssetRequest : AssetMasterRequest
{
    public long BranchId { get; init; }
    public long? CurrentDepartmentId { get; init; }
}
