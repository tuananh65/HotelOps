using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StockDocumentLine
{
    public long LineId { get; set; }

    public long DocumentId { get; set; }

    public int LineNo { get; set; }

    public long ItemId { get; set; }

    public long UomId { get; set; }

    public decimal ConversionFactor { get; set; }

    public decimal? RequestedQty { get; set; }

    public decimal ActualQty { get; set; }

    public decimal BaseQty { get; set; }

    public decimal? UnitCost { get; set; }

    public string? Note { get; set; }

    public virtual StockDocument Document { get; set; } = null!;

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public virtual StockReceiptLineDetail? StockReceiptLineDetail { get; set; }

    public virtual ICollection<StockReturnLineDetail> StockReturnLineDetailOriginalIssueLines { get; set; } = new List<StockReturnLineDetail>();

    public virtual StockReturnLineDetail? StockReturnLineDetailStockDocumentLine { get; set; }

    public virtual ICollection<StockTransferReceiptLine> StockTransferReceiptLines { get; set; } = new List<StockTransferReceiptLine>();

    public virtual UnitsOfMeasure Uom { get; set; } = null!;

    public virtual MaterialFulfillmentLine? MaterialFulfillmentLine { get; set; }

    public virtual MaterialReplenishmentReceiptLine? MaterialReplenishmentReceiptLine { get; set; }

    public virtual ICollection<MaintenanceMaterialUsage> MaintenanceMaterialUsages { get; set; } = new List<MaintenanceMaterialUsage>();
}
