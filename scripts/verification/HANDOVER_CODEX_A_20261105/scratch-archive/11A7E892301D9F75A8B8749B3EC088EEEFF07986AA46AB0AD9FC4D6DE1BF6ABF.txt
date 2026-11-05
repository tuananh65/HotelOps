using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class OperationalTaskAssignment
{
    public long AssignmentId { get; set; }

    public long TaskId { get; set; }

    public long AssigneeId { get; set; }

    public long AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string AssignmentStatus { get; set; } = null!;

    public string? Note { get; set; }

    public virtual User AssignedByNavigation { get; set; } = null!;

    public virtual User Assignee { get; set; } = null!;

    public virtual OperationalTask Task { get; set; } = null!;
}
