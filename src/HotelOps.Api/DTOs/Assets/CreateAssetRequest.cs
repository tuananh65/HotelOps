namespace HotelOps.Api.DTOs.Assets;

public sealed class CreateAssetRequest : AssetMasterRequest
{
    public IReadOnlyList<HotelOps.Api.DTOs.Housekeeping.EvidenceUpload>? Photos { get; init; }
    public long BranchId { get; init; }
    public long? CurrentDepartmentId { get; init; }
}
