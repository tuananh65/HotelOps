namespace HotelOps.Api.DTOs.Assets;

public sealed class ChangeAssetConditionRequest : AssetActorRequest
{
    public string ExpectedCondition { get; init; } = string.Empty;
    public string NewCondition { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public long? RelatedIncidentId { get; init; }
}
