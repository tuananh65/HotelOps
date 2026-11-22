using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class WorkInterval
{
    public long IntervalId { get; set; }

    public long BranchId { get; set; }

    public long WorkId { get; set; }

    public long AssignmentId { get; set; }

    public long UserId { get; set; }

    public long OpenedBy { get; set; }

    public DateTime OpenedAt { get; set; }

    public long? ClosedBy { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? CloseReason { get; set; }

    public DateTime EffectiveStartedAt { get; set; }

    public DateTime? EffectiveEndedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;






}
