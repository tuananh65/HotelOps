using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class VWorkDuration
{
    public long BranchId { get; set; }

    public long WorkId { get; set; }

    public long UserId { get; set; }

    public long? CompletedSeconds { get; set; }

    public int? OpenIntervals { get; set; }
}
