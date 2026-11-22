using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class WorkEvent
{
    public long WorkEventId { get; set; }

    public long BranchId { get; set; }

    public long WorkId { get; set; }

    public string EventType { get; set; } = null!;

    public long? RecipientUserId { get; set; }

    public long? RelatedEventId { get; set; }

    public long ActorUserId { get; set; }

    public Guid OperationKey { get; set; }

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public decimal? ProgressPercent { get; set; }

    public string? Reason { get; set; }

    public string? SnapshotJson { get; set; }

    public DateTime OccurredAt { get; set; }






}
