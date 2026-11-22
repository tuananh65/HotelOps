using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class RoomEvent
{
    public long RoomEventId { get; set; }

    public long BranchId { get; set; }

    public long RoomId { get; set; }

    public long? SourceWorkId { get; set; }

    public int TurnoverNo { get; set; }

    public string EventType { get; set; } = null!;

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public long ActorUserId { get; set; }

    public Guid OperationKey { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }




}
