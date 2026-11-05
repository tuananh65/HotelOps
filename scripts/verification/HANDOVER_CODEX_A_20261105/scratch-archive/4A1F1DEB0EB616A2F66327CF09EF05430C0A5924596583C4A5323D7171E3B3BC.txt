using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class ShiftTemplate
{
    public long ShiftTemplateId { get; set; }

    public long BranchId { get; set; }

    public string ShiftCode { get; set; } = null!;

    public string ShiftName { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int BreakMinutes { get; set; }

    public bool CrossesMidnight { get; set; }

    public bool IsActive { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();
}
