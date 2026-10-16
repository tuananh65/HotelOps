using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class OperationalTaskProgress
{
    public long ProgressId { get; set; }

    public long TaskId { get; set; }

    public int ProgressPercent { get; set; }

    public string? OldStatus { get; set; }

    public string? NewStatus { get; set; }

    public string? Description { get; set; }

    public string? BlockerReason { get; set; }

    public long ReportedBy { get; set; }

    public DateTime ReportedAt { get; set; }

    public virtual User ReportedByNavigation { get; set; } = null!;

    public virtual OperationalTask Task { get; set; } = null!;
}
