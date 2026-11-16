namespace HotelOps.Api.DTOs.Incidents;

public sealed class IncidentQueryRequest
{
    // Query filter only; this does not enforce branch access.
    public long? BranchId { get; init; }
    public string? Status { get; init; }
    public long? PriorityId { get; init; }
    public long? SeverityId { get; init; }
    public long? AssigneeId { get; init; }
    public long? CategoryId { get; init; }
    public long? RoomId { get; init; }
    public long? AreaId { get; init; }
    public long? AssetId { get; init; }
    public bool? Overdue { get; init; }
    public bool SimilarOpenOnly { get; init; }
    public string? Keyword { get; init; }
    public DateTimeOffset? DetectedFrom { get; init; }
    public DateTimeOffset? DetectedTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
