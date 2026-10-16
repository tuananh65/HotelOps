using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class WorkShift
{
    public long ShiftId { get; set; }

    public long BranchId { get; set; }

    public long DepartmentId { get; set; }

    public long ShiftTemplateId { get; set; }

    public DateOnly ShiftDate { get; set; }

    public DateTime ScheduledStartAt { get; set; }

    public DateTime ScheduledEndAt { get; set; }

    public int RequiredStaffCount { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<ShiftAssignment> ShiftAssignments { get; set; } = new List<ShiftAssignment>();

    public virtual ShiftTemplate ShiftTemplate { get; set; } = null!;
}
