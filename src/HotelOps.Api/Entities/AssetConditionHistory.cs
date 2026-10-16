using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class AssetConditionHistory
{
    public long ConditionHistoryId { get; set; }

    public long AssetId { get; set; }

    public string? OldCondition { get; set; }

    public string NewCondition { get; set; } = null!;

    public string? Note { get; set; }

    public long? RelatedIncidentId { get; set; }

    public long ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    public virtual Asset Asset { get; set; } = null!;

    public virtual User ChangedByNavigation { get; set; } = null!;

    public virtual Incident? RelatedIncident { get; set; }
}
