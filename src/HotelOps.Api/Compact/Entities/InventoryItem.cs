using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class InventoryItem
{
    public long ItemId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long? CategoryId { get; set; }

    public long BaseUnitId { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public string CategoryKind { get; set; } = null!;

    public string BaseUnitKind { get; set; } = null!;




}
