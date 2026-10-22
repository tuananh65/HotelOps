namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetHandoverDto
{
    public long HandoverId { get; init; }
    public string HandoverNo { get; init; } = string.Empty;
    public long AssetId { get; init; }
    public string AssetCode { get; init; } = string.Empty;
    public long? FromDepartmentId { get; init; }
    public string? FromDepartmentName { get; init; }
    public long? ToDepartmentId { get; init; }
    public string? ToDepartmentName { get; init; }
    public long? FromUserId { get; init; }
    public string? FromUserName { get; init; }
    public long? ToUserId { get; init; }
    public string? ToUserName { get; init; }
    public string? ConditionNote { get; init; }
    public string HandoverStatus { get; init; } = string.Empty;
    public DateTime? HandedOverAt { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public long CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
}
