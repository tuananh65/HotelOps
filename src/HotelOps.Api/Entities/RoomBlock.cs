using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class RoomBlock
{
    public long BlockId { get; set; }

    public long RoomId { get; set; }

    public long? IncidentId { get; set; }

    public string Reason { get; set; } = null!;

    public DateTime BlockedAt { get; set; }

    public long BlockedBy { get; set; }

    public DateTime? ExpectedReleaseAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public long? ReleasedBy { get; set; }

    public string? ReleaseNote { get; set; }

    public virtual User BlockedByNavigation { get; set; } = null!;

    public virtual Incident? Incident { get; set; }

    public virtual User? ReleasedByNavigation { get; set; }

    public virtual Room Room { get; set; } = null!;
}
