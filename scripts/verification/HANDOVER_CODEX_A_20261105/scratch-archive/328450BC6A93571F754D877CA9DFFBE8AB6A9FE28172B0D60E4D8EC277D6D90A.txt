using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class IncidentEvent
{
    public long EventId { get; set; }

    public long IncidentId { get; set; }

    public string EventType { get; set; } = null!;

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public string? Note { get; set; }

    public long ActorId { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual User Actor { get; set; } = null!;

    public virtual Incident Incident { get; set; } = null!;
}
