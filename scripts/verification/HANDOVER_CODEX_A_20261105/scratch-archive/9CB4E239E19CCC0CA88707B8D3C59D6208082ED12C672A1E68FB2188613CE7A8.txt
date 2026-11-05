using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class AuditLog
{
    public long AuditId { get; set; }

    public long? BranchId { get; set; }

    public long? ActorUserId { get; set; }

    public string EntityType { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? ChangedFields { get; set; }

    public string? IpAddress { get; set; }

    public string? RequestId { get; set; }

    public string? UserAgent { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual User? ActorUser { get; set; }

    public virtual Branch? Branch { get; set; }
}
