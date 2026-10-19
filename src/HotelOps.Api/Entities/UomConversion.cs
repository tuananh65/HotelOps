using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class UomConversion
{
    public long ConversionId { get; set; }

    public long? ItemId { get; set; }

    public long FromUomId { get; set; }

    public long ToUomId { get; set; }

    public decimal ConversionFactor { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual UnitsOfMeasure FromUom { get; set; } = null!;

    public virtual InventoryItem? Item { get; set; }

    public virtual UnitsOfMeasure ToUom { get; set; } = null!;
}
