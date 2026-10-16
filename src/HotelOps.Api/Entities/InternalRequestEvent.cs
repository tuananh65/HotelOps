using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InternalRequestEvent
{
    public long EventId { get; set; }

    public long InternalRequestId { get; set; }

    public string EventType { get; set; } = null!;

    public string? OldStatus { get; set; }

    public string? NewStatus { get; set; }

    public string? EventNote { get; set; }

    public long ActorId { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual User Actor { get; set; } = null!;

    public virtual InternalRequest InternalRequest { get; set; } = null!;
}
