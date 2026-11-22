using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class InventoryPolicy
{
    public long InventoryPolicyId { get; set; }

    public long BranchId { get; set; }

    public long WarehouseId { get; set; }

    public long ItemId { get; set; }

    public decimal MinimumQty { get; set; }

    public decimal TargetQty { get; set; }

    public Guid? LowStockEpisode { get; set; }

    public DateTime? AlertedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;



}
