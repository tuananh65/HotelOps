using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class WorkLink
{
    public long WorkLinkId { get; set; }

    public long BranchId { get; set; }

    public long SourceWorkId { get; set; }

    public long TargetWorkId { get; set; }

    public string Relation { get; set; } = null!;

    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }




}
