using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class ShiftAttendance
{
    public long AttendanceId { get; set; }

    public long ShiftAssignmentId { get; set; }

    public DateTime? ActualCheckInAt { get; set; }

    public DateTime? ActualCheckOutAt { get; set; }

    public string AttendanceStatus { get; set; } = null!;

    public int? LateMinutes { get; set; }

    public int? OvertimeMinutes { get; set; }

    public string? Note { get; set; }

    public long? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public virtual ShiftAssignment ShiftAssignment { get; set; } = null!;

    public virtual User? VerifiedByNavigation { get; set; }
}
