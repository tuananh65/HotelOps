using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class AssetType
{
    public long AssetTypeId { get; set; }

    public string TypeCode { get; set; } = null!;

    public string TypeName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
