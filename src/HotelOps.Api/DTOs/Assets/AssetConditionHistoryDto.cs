namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetConditionHistoryDto
{
    public long ConditionHistoryId { get; init; }
    public long AssetId { get; init; }
    public string? OldCondition { get; init; }
    public string NewCondition { get; init; } = string.Empty;
    public string? Note { get; init; }
    public long? RelatedIncidentId { get; init; }
    public long ChangedBy { get; init; }
    public DateTime ChangedAt { get; init; }
}
