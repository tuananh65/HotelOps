namespace HotelOps.Api.DTOs.Maintenance;

public sealed class MaintenanceQueryRequest
{
    public long? IncidentId { get; init; }
    public long? AssigneeId { get; init; }
    public long? DepartmentId { get; init; }
    public string? Status { get; init; }
    public long? PriorityId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
