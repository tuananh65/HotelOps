using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class StocktakeLine
{
    public long StocktakeLineId { get; set; }

    public long StocktakeId { get; set; }

    public long ItemId { get; set; }

    public decimal SystemQty { get; set; }

    public decimal? CountedQty { get; set; }

    public decimal? VarianceQty { get; set; }

    public string? DiscrepancyReason { get; set; }

    public DateTime? CountedAt { get; set; }

    public long? CountedBy { get; set; }

    public virtual User? CountedByNavigation { get; set; }

    public virtual InventoryItem Item { get; set; } = null!;

    public virtual Stocktake Stocktake { get; set; } = null!;
}
