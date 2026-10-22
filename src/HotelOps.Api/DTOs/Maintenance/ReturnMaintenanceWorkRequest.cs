using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class ReturnMaintenanceWorkRequest : MaintenanceActorRequest
{
    [Required]
    public string Reason { get; init; } = string.Empty;
}
