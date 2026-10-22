using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class CreateMaintenanceWorkRequest : MaintenanceActorRequest
{
    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;
    public string? Instruction { get; init; }
    public long? DepartmentId { get; init; }
    public long? PriorityId { get; init; }
    public DateTime? PlannedStartAt { get; init; }
    public DateTime? PlannedEndAt { get; init; }
}
