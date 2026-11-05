using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialReplenishmentLine
{
    public long ReplenishmentLineId { get; set; }

    public long ReplenishmentId { get; set; }

    public long WarehouseId { get; set; }

    public int LineNo { get; set; }

    public long ItemId { get; set; }

    public long BaseUomId { get; set; }

    public decimal RequestedBaseQty { get; set; }

    public decimal ReceivedBaseQty { get; set; }

    public string SourceType { get; set; } = null!;

    public long? SourceThresholdId { get; set; }

    public long? SourceMaterialRequestLineId { get; set; }

    public bool IsOpenCoverage { get; set; }

    public string? Note { get; set; }

    public virtual UnitsOfMeasure BaseUom { get; set; } = null!;

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual MaterialReplenishment MaterialReplenishment { get; set; } = null!;

    public virtual ICollection<MaterialReplenishmentReceiptLine> MaterialReplenishmentReceiptLines { get; set; } = new List<MaterialReplenishmentReceiptLine>();

    public virtual MaterialRequestLine? SourceMaterialRequestLine { get; set; }

    public virtual StockThreshold? SourceThreshold { get; set; }
}
