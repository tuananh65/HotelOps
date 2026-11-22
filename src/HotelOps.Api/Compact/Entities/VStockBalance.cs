using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class VStockBalance
{
    public long BranchId { get; set; }

    public long WarehouseId { get; set; }

    public long ItemId { get; set; }

    public decimal? BaseQty { get; set; }
}
