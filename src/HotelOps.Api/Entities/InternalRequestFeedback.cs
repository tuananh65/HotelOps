using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InternalRequestFeedback
{
    public long FeedbackId { get; set; }

    public long InternalRequestId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public bool? IsSatisfied { get; set; }

    public long SubmittedBy { get; set; }

    public DateTime SubmittedAt { get; set; }

    public virtual InternalRequest InternalRequest { get; set; } = null!;

    public virtual User SubmittedByNavigation { get; set; } = null!;
}
