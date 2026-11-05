using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InternalRequestAssignment
{
    public long AssignmentId { get; set; }

    public long InternalRequestId { get; set; }

    public long AssigneeId { get; set; }

    public long AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string Status { get; set; } = null!;

    public string? AssignmentNote { get; set; }

    public virtual User AssignedByNavigation { get; set; } = null!;

    public virtual User Assignee { get; set; } = null!;

    public virtual InternalRequest InternalRequest { get; set; } = null!;
}
