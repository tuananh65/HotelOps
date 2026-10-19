using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StockThreshold
{
    public long ThresholdId { get; set; }

    public long WarehouseId { get; set; }

    public long ItemId { get; set; }

    public decimal MinimumQty { get; set; }

    public decimal? MaximumQty { get; set; }

    public decimal? ReorderQty { get; set; }

    public bool AlertEnabled { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
