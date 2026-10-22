namespace HotelOps.Api.DTOs.Maintenance;

public sealed class AssignMaintenanceWorkRequest : MaintenanceActorRequest
{
    public long AssigneeId { get; init; }
    // Null/omitted means preserve the current value; clearing is not supported here.
    public string? Instruction { get; init; }
    public DateTime? PlannedEndAt { get; init; }
}
