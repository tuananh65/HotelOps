using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StockReceiptLineDetail
{
    public long StockDocumentLineId { get; set; }

    public decimal RejectedQty { get; set; }

    public decimal DamagedQty { get; set; }

    public string? DiscrepancyReason { get; set; }

    public virtual StockDocumentLine StockDocumentLine { get; set; } = null!;
}
