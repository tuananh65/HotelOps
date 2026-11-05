using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class UnitsOfMeasure
{
    public long UomId { get; set; }

    public string UomCode { get; set; } = null!;

    public string UomName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

    public virtual ICollection<MaterialFulfillmentLine> MaterialFulfillmentLines { get; set; } = new List<MaterialFulfillmentLine>();

    public virtual ICollection<MaterialRequestLine> MaterialRequestLines { get; set; } = new List<MaterialRequestLine>();

    public virtual ICollection<StockDocumentLine> StockDocumentLines { get; set; } = new List<StockDocumentLine>();

    public virtual ICollection<UomConversion> UomConversionFromUoms { get; set; } = new List<UomConversion>();

    public virtual ICollection<UomConversion> UomConversionToUoms { get; set; } = new List<UomConversion>();

    public virtual ICollection<MaterialRequestApprovalLineSnapshot> MaterialRequestApprovalLineSnapshots { get; set; } = new List<MaterialRequestApprovalLineSnapshot>();

    public virtual ICollection<MaterialReplenishmentLine> MaterialReplenishmentLines { get; set; } = new List<MaterialReplenishmentLine>();

    public virtual ICollection<MaintenanceMaterialUsage> MaintenanceMaterialUsageBaseUoms { get; set; } = new List<MaintenanceMaterialUsage>();

    public virtual ICollection<MaintenanceMaterialUsage> MaintenanceMaterialUsageUoms { get; set; } = new List<MaintenanceMaterialUsage>();
}
