using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class ShiftAssignment
{
    public long ShiftAssignmentId { get; set; }

    public long ShiftId { get; set; }

    public long UserId { get; set; }

    public string AssignmentStatus { get; set; } = null!;

    public long AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public string? CancellationReason { get; set; }

    public virtual User AssignedByNavigation { get; set; } = null!;

    public virtual WorkShift Shift { get; set; } = null!;

    public virtual ShiftAttendance? ShiftAttendance { get; set; }

    public virtual User User { get; set; } = null!;
}
