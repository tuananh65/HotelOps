using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialReplenishment
{
    public long ReplenishmentId { get; set; }

    public string ReplenishmentNo { get; set; } = null!;

    public long WarehouseId { get; set; }

    public long RequestedBy { get; set; }

    public string? SupplierName { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? ClosedAt { get; set; }

    public long? ClosedBy { get; set; }

    public string? CloseReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual User? ClosedByNavigation { get; set; }

    public virtual ICollection<MaterialReplenishmentLine> MaterialReplenishmentLines { get; set; } = new List<MaterialReplenishmentLine>();

    public virtual User RequestedByNavigation { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
