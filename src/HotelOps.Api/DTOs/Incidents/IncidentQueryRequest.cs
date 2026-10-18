namespace HotelOps.Api.DTOs.Incidents;

public sealed class IncidentQueryRequest
{
    // Query filter only; this does not enforce branch access.
    public long? BranchId { get; init; }
    public string? Status { get; init; }
    public long? PriorityId { get; init; }
    public long? SeverityId { get; init; }
    public long? RoomId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
