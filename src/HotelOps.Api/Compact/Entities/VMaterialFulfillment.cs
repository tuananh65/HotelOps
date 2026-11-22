using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class VMaterialFulfillment
{
    public long DocumentId { get; set; }

    public long BranchId { get; set; }

    public long LineId { get; set; }

    public long ItemId { get; set; }

    public decimal? RequestedQty { get; set; }

    public decimal? ApprovedQty { get; set; }

    public decimal ConversionFactor { get; set; }

    public decimal? IssuedBaseQty { get; set; }

    public decimal? MissingBaseQty { get; set; }
}
