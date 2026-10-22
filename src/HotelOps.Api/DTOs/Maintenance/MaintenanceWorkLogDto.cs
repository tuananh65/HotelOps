namespace HotelOps.Api.DTOs.Maintenance;

public sealed class MaintenanceWorkLogDto
{
    public long LogId { get; init; }
    public long MaintenanceWorkId { get; init; }
    public string WorkNote { get; init; } = string.Empty;
    public int SpentMinutes { get; init; }
    public long? MaterialIssueId { get; init; }
    public string? Status { get; init; }
    public long LoggedBy { get; init; }
    public DateTime LoggedAt { get; init; }
    public IReadOnlyList<MaintenanceMaterialUsageDto> MaterialUsages { get; set; } = [];
}
