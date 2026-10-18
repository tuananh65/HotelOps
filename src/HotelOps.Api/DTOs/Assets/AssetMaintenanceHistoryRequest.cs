namespace HotelOps.Api.DTOs.Assets;

public sealed class AssetMaintenanceHistoryRequest : AssetPageRequest
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    // CORRECTIVE_WORK, PREVENTIVE_RUN, CONDITION_CHANGE; blank means all.
    public string? Type { get; init; }
}
