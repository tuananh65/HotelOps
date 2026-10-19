using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StockDocument
{
    public long DocumentId { get; set; }

    public string DocumentNo { get; set; } = null!;

    public string DocumentType { get; set; } = null!;

    public long? SourceWarehouseId { get; set; }

    public long? DestinationWarehouseId { get; set; }

    public string Status { get; set; } = null!;

    public string? ReferenceType { get; set; }

    public long? ReferenceId { get; set; }

    public DateOnly DocumentDate { get; set; }

    public string? Reason { get; set; }

    public string? Note { get; set; }

    public long CreatedBy { get; set; }

    public long? ApprovedBy { get; set; }

    public long? PostedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PostedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public long? ReversalOfDocumentId { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Warehouse? DestinationWarehouse { get; set; }

    public virtual ICollection<StockDocument> InverseReversalOfDocument { get; set; } = new List<StockDocument>();

    public virtual ICollection<MaintenanceWorkLog> MaintenanceWorkLogs { get; set; } = new List<MaintenanceWorkLog>();

    public virtual ICollection<MaterialFulfillment> MaterialFulfillments { get; set; } = new List<MaterialFulfillment>();

    public virtual User? PostedByNavigation { get; set; }

    public virtual StockDocument? ReversalOfDocument { get; set; }

    public virtual Warehouse? SourceWarehouse { get; set; }

    public virtual ICollection<StockDocumentLine> StockDocumentLines { get; set; } = new List<StockDocumentLine>();

    public virtual ICollection<StockTransferReceipt> StockTransferReceipts { get; set; } = new List<StockTransferReceipt>();

    public virtual ICollection<Stocktake> Stocktakes { get; set; } = new List<Stocktake>();
}
