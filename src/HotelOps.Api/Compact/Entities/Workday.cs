namespace HotelOps.Api.Compact.Entities;

// Database-first mappings for the additive 06_Workday.sql release.
public sealed class WorkdayShift
{
    public long ShiftId { get; set; }
    public long BranchId { get; set; }
    public long DepartmentId { get; set; }
    public long RoleId { get; set; }
    public string Name { get; set; } = "";
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class WorkdayAssignment
{
    public long AssignmentId { get; set; }
    public long ShiftId { get; set; }
    public long UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Status { get; set; } = "PLANNED";
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class WorkdayEvent
{
    public long EventId { get; set; }
    public long AssignmentId { get; set; }
    public long ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public Guid OperationKey { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }
}
