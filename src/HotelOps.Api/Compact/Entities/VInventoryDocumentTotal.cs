using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class VInventoryDocumentTotal
{
    public long DocumentId { get; set; }

    public long BranchId { get; set; }

    public string Kind { get; set; } = null!;

    public string Status { get; set; } = null!;

    public long? LineCount { get; set; }

    public decimal? BaseQty { get; set; }

    public decimal? TotalCost { get; set; }
}
