using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class VerifyMaintenanceWorkRequest : MaintenanceActorRequest
{
    [Required]
    public string Note { get; init; } = string.Empty;
}
