using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class CancelMaintenanceWorkRequest : MaintenanceActorRequest
{
    [Required]
    public string Reason { get; init; } = string.Empty;
}
