using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialRequestApprovalLineSnapshot
{
    public long SnapshotId { get; set; }

    public long ApprovalCycleId { get; set; }

    public long RequestLineId { get; set; }

    public int LineNo { get; set; }

    public long ItemId { get; set; }

    public long UomId { get; set; }

    public decimal RequestedQty { get; set; }

    public decimal? ApprovedQty { get; set; }

    public string? Note { get; set; }

    public virtual MaterialRequestApprovalCycle ApprovalCycle { get; set; } = null!;

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual MaterialRequestLine RequestLine { get; set; } = null!;

    public virtual UnitsOfMeasure Uom { get; set; } = null!;
}
