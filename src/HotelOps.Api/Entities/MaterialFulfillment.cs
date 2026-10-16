using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialFulfillment
{
    public long FulfillmentId { get; set; }

    public string FulfillmentNo { get; set; } = null!;

    public long RequestId { get; set; }

    public long? StockDocumentId { get; set; }

    public long WarehouseId { get; set; }

    public long FulfilledBy { get; set; }

    public long? ReceivedBy { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? FulfilledAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User FulfilledByNavigation { get; set; } = null!;

    public virtual ICollection<MaterialFulfillmentLine> MaterialFulfillmentLines { get; set; } = new List<MaterialFulfillmentLine>();

    public virtual User? ReceivedByNavigation { get; set; }

    public virtual MaterialRequest Request { get; set; } = null!;

    public virtual StockDocument? StockDocument { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}
