namespace HotelOps.Api.DTOs.Assets;

public sealed class CreateAssetHandoverRequest : AssetActorRequest
{
    public long ToDepartmentId { get; init; }
    public long ToUserId { get; init; }
    public string? ConditionNote { get; init; }
}
