using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialRequestLine
{
    public long LineId { get; set; }

    public long RequestId { get; set; }

    public int LineNo { get; set; }

    public long ItemId { get; set; }

    public long UomId { get; set; }

    public decimal RequestedQty { get; set; }

    public decimal? ApprovedQty { get; set; }

    public decimal FulfilledQty { get; set; }

    public string? Note { get; set; }

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual ICollection<MaterialFulfillmentLine> MaterialFulfillmentLines { get; set; } = new List<MaterialFulfillmentLine>();

    public virtual MaterialRequest Request { get; set; } = null!;

    public virtual UnitsOfMeasure Uom { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<MaterialRequestApprovalLineSnapshot> MaterialRequestApprovalLineSnapshots { get; set; } = new List<MaterialRequestApprovalLineSnapshot>();

    public virtual ICollection<MaterialReplenishmentLine> MaterialReplenishmentLines { get; set; } = new List<MaterialReplenishmentLine>();
}
