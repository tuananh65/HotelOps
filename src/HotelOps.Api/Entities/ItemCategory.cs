using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class ItemCategory
{
    public long ItemCategoryId { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
}
