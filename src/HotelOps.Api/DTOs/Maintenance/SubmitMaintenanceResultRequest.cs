using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class SubmitMaintenanceResultRequest : MaintenanceActorRequest
{
    [Required]
    public string Diagnosis { get; init; } = string.Empty;

    [Required]
    public string ActionTaken { get; init; } = string.Empty;

    public string? ResolutionNote { get; init; }
}
