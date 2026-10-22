using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class HoldMaintenanceWorkRequest : MaintenanceActorRequest
{
    [Required]
    public string Reason { get; init; } = string.Empty;
}
