using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialReplenishmentReceiptLine
{
    public long ReplenishmentReceiptLineId { get; set; }

    public long ReplenishmentLineId { get; set; }

    public long StockDocumentLineId { get; set; }

    public decimal CreditedBaseQty { get; set; }

    public DateTime LinkedAt { get; set; }

    public long LinkedBy { get; set; }

    public virtual User LinkedByNavigation { get; set; } = null!;

    public virtual MaterialReplenishmentLine ReplenishmentLine { get; set; } = null!;

    public virtual StockDocumentLine StockDocumentLine { get; set; } = null!;
}
