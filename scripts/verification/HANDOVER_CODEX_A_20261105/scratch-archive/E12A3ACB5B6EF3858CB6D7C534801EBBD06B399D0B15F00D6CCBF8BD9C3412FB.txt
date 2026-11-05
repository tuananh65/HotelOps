using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StockMovement
{
    public long MovementId { get; set; }

    public long DocumentLineId { get; set; }

    public long WarehouseId { get; set; }

    public long ItemId { get; set; }

    public decimal SignedBaseQty { get; set; }

    public decimal? UnitCost { get; set; }

    public DateTime OccurredAt { get; set; }

    public long CreatedBy { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual StockDocumentLine DocumentLine { get; set; } = null!;

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
