using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class SubmitMaintenanceResultRequest : MaintenanceActorRequest
{
    [Required]
    public string ResolutionNote { get; init; } = string.Empty;
}
