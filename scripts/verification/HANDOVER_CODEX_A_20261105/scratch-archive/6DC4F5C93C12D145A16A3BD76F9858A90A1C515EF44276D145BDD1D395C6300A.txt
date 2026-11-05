using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaintenanceMaterialUsage
{
    public long UsageId { get; set; }

    public long MaintenanceWorkId { get; set; }

    public long MaintenanceWorkLogId { get; set; }

    public long StockDocumentLineId { get; set; }

    public long ItemId { get; set; }

    public long BaseUomId { get; set; }

    public long UomId { get; set; }

    public decimal UsedQty { get; set; }

    public decimal ConversionFactor { get; set; }

    public decimal UsedBaseQty { get; set; }

    public virtual UnitsOfMeasure BaseUom { get; set; } = null!;

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual MaintenanceWork MaintenanceWork { get; set; } = null!;

    public virtual MaintenanceWorkLog MaintenanceWorkLog { get; set; } = null!;

    public virtual StockDocumentLine StockDocumentLine { get; set; } = null!;

    public virtual UnitsOfMeasure Uom { get; set; } = null!;
}
