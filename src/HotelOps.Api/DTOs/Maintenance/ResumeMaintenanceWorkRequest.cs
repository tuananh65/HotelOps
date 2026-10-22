using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class ResumeMaintenanceWorkRequest : MaintenanceActorRequest
{
    public string? Note { get; init; }
}
