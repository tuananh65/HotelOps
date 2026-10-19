using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Stocktake
{
    public long StocktakeId { get; set; }

    public string StocktakeNo { get; set; } = null!;

    public long WarehouseId { get; set; }

    public DateTime SnapshotAt { get; set; }

    public string Status { get; set; } = null!;

    public long? CountedBy { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public long? AdjustmentDocumentId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual StockDocument? AdjustmentDocument { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User? CountedByNavigation { get; set; }

    public virtual ICollection<StocktakeLine> StocktakeLines { get; set; } = new List<StocktakeLine>();

    public virtual Warehouse Warehouse { get; set; } = null!;
}
