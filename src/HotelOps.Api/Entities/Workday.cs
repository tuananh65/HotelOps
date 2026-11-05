namespace HotelOps.Api.Entities;

// Proposed Database First model of Task77 DDL; not active in the production model.
public partial class Workday
{
    public long WorkdayId { get; set; }
    public long UserId { get; set; }
    public long BranchId { get; set; }
    public long DepartmentId { get; set; }
    public DateOnly WorkDate { get; set; }
    public long? ShiftAssignmentId { get; set; }
    public bool IsUnscheduled { get; set; }
    public string? UnscheduledReason { get; set; }
    public string Status { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
