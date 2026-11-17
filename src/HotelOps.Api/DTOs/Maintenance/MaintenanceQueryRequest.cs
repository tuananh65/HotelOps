namespace HotelOps.Api.DTOs.Maintenance;

public sealed class MaintenanceQueryRequest
{
    public long? BranchId { get; init; }
    public long? IncidentId { get; init; }
    public long? AssigneeId { get; init; }
    public long? DepartmentId { get; init; }
    public string? Keyword { get; init; }
    public string? Source { get; init; }
    public string? Status { get; init; }
    public long? PriorityId { get; init; }
    public DateTimeOffset? DueFrom { get; init; }
    public DateTimeOffset? DueTo { get; init; }
    public bool? Overdue { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
