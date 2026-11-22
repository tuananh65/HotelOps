using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class VBacklogHandover
{
    public long WorkEventId { get; set; }

    public long BranchId { get; set; }

    public long WorkId { get; set; }

    public long SenderUserId { get; set; }

    public long? RecipientUserId { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }

    public string? Decision { get; set; }

    public DateTime? DecidedAt { get; set; }
}
