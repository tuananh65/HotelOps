using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StockTransferReceiptLine
{
    public long TransferReceiptLineId { get; set; }

    public long TransferReceiptId { get; set; }

    public long TransferLineId { get; set; }

    public decimal ReceivedQty { get; set; }

    public decimal DamagedQty { get; set; }

    public decimal MissingQty { get; set; }

    public string? Note { get; set; }

    public virtual StockDocumentLine TransferLine { get; set; } = null!;

    public virtual StockTransferReceipt TransferReceipt { get; set; } = null!;
}
