using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class AuditLog
{
    public long AuditId { get; set; }

    public long? BranchId { get; set; }

    public long? ActorUserId { get; set; }

    public Guid OperationKey { get; set; }

    public string EntityType { get; set; } = null!;

    public string EntityKey { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public DateTime OccurredAt { get; set; }


}
