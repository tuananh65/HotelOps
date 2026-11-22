using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class StockMovement
{
    public long MovementId { get; set; }

    public long BranchId { get; set; }

    public long LineId { get; set; }

    public long WarehouseId { get; set; }

    public long ItemId { get; set; }

    public decimal SignedBaseQty { get; set; }

    public decimal? UnitCost { get; set; }

    public long? ReversalOfMovementId { get; set; }

    public long CreatedBy { get; set; }

    public DateTime OccurredAt { get; set; }






}
