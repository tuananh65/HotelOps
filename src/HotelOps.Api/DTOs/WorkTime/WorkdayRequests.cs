using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.WorkTime;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StartWorkdayRequest
{
    [Range(1, long.MaxValue)] public long? ShiftAssignmentId { get; init; }
    [StringLength(1000)] public string? Reason { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EndWorkdayRequest
{
    [Required] public bool? PauseAll { get; init; }
}

public sealed record WorkdayResponse(long WorkdayId, long BranchId, long DepartmentId,
    DateOnly WorkDate, long? ShiftAssignmentId, bool IsUnscheduled, string Status,
    DateTimeOffset StartedAtUtc, DateTimeOffset? EndedAtUtc, string RowVersion);

public sealed record OwnEligibleShift(long ShiftAssignmentId, long ShiftId, long BranchId,
    long DepartmentId, DateOnly ShiftDate, DateTimeOffset ScheduledStartAtUtc, DateTimeOffset ScheduledEndAtUtc);
