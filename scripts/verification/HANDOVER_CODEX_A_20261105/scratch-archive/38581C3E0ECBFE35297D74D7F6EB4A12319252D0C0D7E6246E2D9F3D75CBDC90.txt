using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class CleaningAssignment
{
    public long AssignmentId { get; set; }

    public long CleaningTaskId { get; set; }

    public long AssigneeUserId { get; set; }

    public long AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string AssignmentStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public virtual User AssignedByNavigation { get; set; } = null!;

    public virtual User AssigneeUser { get; set; } = null!;

    public virtual CleaningTask CleaningTask { get; set; } = null!;
}
