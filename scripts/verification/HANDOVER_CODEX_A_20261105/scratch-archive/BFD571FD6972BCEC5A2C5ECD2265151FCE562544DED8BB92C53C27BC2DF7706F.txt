using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class AssetLocationHistory
{
    public long LocationHistoryId { get; set; }

    public long AssetId { get; set; }

    public string? FromLocationType { get; set; }

    public long? FromLocationId { get; set; }

    public string ToLocationType { get; set; } = null!;

    public long ToLocationId { get; set; }

    public DateTime EffectiveAt { get; set; }

    public long MovedBy { get; set; }

    public string? Reason { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual User MovedByNavigation { get; set; } = null!;
}
