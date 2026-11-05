using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class CleaningProgressLog
{
    public long ProgressId { get; set; }

    public long CleaningTaskId { get; set; }

    public int ProgressPercent { get; set; }

    public string? Status { get; set; }

    public long? BlockerTypeId { get; set; }

    public string? Note { get; set; }

    public long LoggedBy { get; set; }

    public DateTime LoggedAt { get; set; }

    public virtual ReferenceValue? BlockerType { get; set; }

    public virtual CleaningTask CleaningTask { get; set; } = null!;

    public virtual User LoggedByNavigation { get; set; } = null!;
}
