using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class CreateMaintenanceWorkLogRequest : MaintenanceActorRequest
{
    [Required]
    public string WorkNote { get; init; } = string.Empty;
    [Range(0, int.MaxValue)]
    public int SpentMinutes { get; init; }
}
