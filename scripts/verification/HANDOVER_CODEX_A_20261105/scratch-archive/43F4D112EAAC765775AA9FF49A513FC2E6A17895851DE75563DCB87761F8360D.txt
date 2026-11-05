using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class PreventiveMaintenanceRun
{
    public long RunId { get; set; }

    public long ScheduleId { get; set; }

    public long? MaintenanceWorkId { get; set; }

    public DateTime PlannedAt { get; set; }

    public DateTime? PerformedAt { get; set; }

    public string? Result { get; set; }

    public DateTime? NextDueAt { get; set; }

    public string? Note { get; set; }

    public long? PerformedBy { get; set; }

    public virtual MaintenanceWork? MaintenanceWork { get; set; }

    public virtual User? PerformedByNavigation { get; set; }

    public virtual PreventiveMaintenanceSchedule Schedule { get; set; } = null!;
}
