using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StockTransferReceipt
{
    public long TransferReceiptId { get; set; }

    public long TransferDocumentId { get; set; }

    public long ReceivedBy { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string? Note { get; set; }

    public virtual User ReceivedByNavigation { get; set; } = null!;

    public virtual ICollection<StockTransferReceiptLine> StockTransferReceiptLines { get; set; } = new List<StockTransferReceiptLine>();

    public virtual StockDocument TransferDocument { get; set; } = null!;
}
