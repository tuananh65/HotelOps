using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class RoomStatusHistory
{
    public long HistoryId { get; set; }

    public long RoomId { get; set; }

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = null!;

    public string? TriggerType { get; set; }

    public long? TriggerId { get; set; }

    public long? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Reason { get; set; }

    public virtual User? ChangedByNavigation { get; set; }

    public virtual Room Room { get; set; } = null!;
}
