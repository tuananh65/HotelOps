using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InventoryItem
{
    public long ItemId { get; set; }

    public string ItemCode { get; set; } = null!;

    public string ItemName { get; set; } = null!;

    public long CategoryId { get; set; }

    public long BaseUomId { get; set; }

    public string? Description { get; set; }

    public string? Specification { get; set; }

    public bool IsConsumable { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual UnitsOfMeasure BaseUom { get; set; } = null!;

    public virtual ItemCategory Category { get; set; } = null!;

    public virtual ICollection<MaterialFulfillmentLine> MaterialFulfillmentLines { get; set; } = new List<MaterialFulfillmentLine>();

    public virtual ICollection<MaterialRequestLine> MaterialRequestLines { get; set; } = new List<MaterialRequestLine>();

    public virtual ICollection<StockDocumentLine> StockDocumentLines { get; set; } = new List<StockDocumentLine>();

    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();

    public virtual ICollection<StockThreshold> StockThresholds { get; set; } = new List<StockThreshold>();

    public virtual ICollection<StocktakeLine> StocktakeLines { get; set; } = new List<StocktakeLine>();

    public virtual ICollection<UomConversion> UomConversions { get; set; } = new List<UomConversion>();

    public virtual ICollection<MaterialRequestApprovalLineSnapshot> MaterialRequestApprovalLineSnapshots { get; set; } = new List<MaterialRequestApprovalLineSnapshot>();

    public virtual ICollection<MaterialReplenishmentLine> MaterialReplenishmentLines { get; set; } = new List<MaterialReplenishmentLine>();

    public virtual ICollection<MaintenanceMaterialUsage> MaintenanceMaterialUsages { get; set; } = new List<MaintenanceMaterialUsage>();
}
