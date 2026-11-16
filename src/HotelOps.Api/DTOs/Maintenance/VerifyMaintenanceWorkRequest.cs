using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.DTOs.Maintenance;

public sealed class VerifyMaintenanceWorkRequest : MaintenanceActorRequest
{
    [Range(1, long.MaxValue)]
    public long SubmissionAuditId { get; init; }
    [Required]
    public string Note { get; init; } = string.Empty;
}
