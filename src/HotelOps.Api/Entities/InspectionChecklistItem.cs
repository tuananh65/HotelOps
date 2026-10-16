using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InspectionChecklistItem
{
    public long ItemId { get; set; }

    public long VersionId { get; set; }

    public string Criterion { get; set; } = null!;

    public string? Description { get; set; }

    public string? PassCondition { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<InspectionDefect> InspectionDefects { get; set; } = new List<InspectionDefect>();

    public virtual ICollection<InspectionResult> InspectionResults { get; set; } = new List<InspectionResult>();

    public virtual InspectionChecklistVersion Version { get; set; } = null!;
}
