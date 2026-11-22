using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class DocumentEvent
{
    public long DocumentEventId { get; set; }

    public long BranchId { get; set; }

    public long DocumentId { get; set; }

    public long? LineId { get; set; }

    public string EventType { get; set; } = null!;

    public int RevisionNo { get; set; }

    public long ActorUserId { get; set; }

    public Guid OperationKey { get; set; }

    public decimal? QuantitySnapshot { get; set; }

    public string? Reason { get; set; }

    public string? SnapshotJson { get; set; }

    public DateTime OccurredAt { get; set; }




}
