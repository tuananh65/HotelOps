using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StockReturnLineDetail
{
    public long StockDocumentLineId { get; set; }

    public long OriginalIssueLineId { get; set; }

    public decimal DamagedQty { get; set; }

    public string? Note { get; set; }

    public virtual StockDocumentLine OriginalIssueLine { get; set; } = null!;

    public virtual StockDocumentLine StockDocumentLine { get; set; } = null!;
}
