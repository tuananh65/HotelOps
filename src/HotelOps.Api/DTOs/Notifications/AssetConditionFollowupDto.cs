namespace HotelOps.Api.DTOs.Notifications;

public sealed record AssetConditionFollowupDto(long AssetId, long BranchId, string AssetName,
    string ConditionAtClosure, string CurrentCondition, DateTime ClosedAtUtc);
