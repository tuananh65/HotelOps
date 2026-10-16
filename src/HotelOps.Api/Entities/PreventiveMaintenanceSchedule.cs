using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class PreventiveMaintenanceSchedule
{
    public long ScheduleId { get; set; }

    public string ScheduleCode { get; set; } = null!;

    public long? AssetId { get; set; }

    public long? RoomId { get; set; }

    public long? AreaId { get; set; }

    public string Frequency { get; set; } = null!;

    public int FrequencyInterval { get; set; }

    public string? Instruction { get; set; }

    public DateTime? LastPerformedAt { get; set; }

    public DateTime NextDueAt { get; set; }

    public long? AssignedDepartmentId { get; set; }

    public bool IsActive { get; set; }

    public long? CreatedBy { get; set; }

    public virtual Area? Area { get; set; }

    public virtual Asset? Asset { get; set; }

    public virtual Department? AssignedDepartment { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<PreventiveMaintenanceRun> PreventiveMaintenanceRuns { get; set; } = new List<PreventiveMaintenanceRun>();

    public virtual Room? Room { get; set; }
}
