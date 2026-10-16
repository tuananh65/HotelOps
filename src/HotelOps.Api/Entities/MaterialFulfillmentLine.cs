using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialFulfillmentLine
{
    public long FulfillmentLineId { get; set; }

    public long FulfillmentId { get; set; }

    public long RequestLineId { get; set; }

    public long ItemId { get; set; }

    public decimal IssuedQty { get; set; }

    public decimal? ReceivedQty { get; set; }

    public long UomId { get; set; }

    public string? Note { get; set; }

    public virtual MaterialFulfillment Fulfillment { get; set; } = null!;

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual MaterialRequestLine RequestLine { get; set; } = null!;

    public virtual UnitsOfMeasure Uom { get; set; } = null!;
}
