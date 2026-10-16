using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InspectionResult
{
    public long ResultId { get; set; }

    public long InspectionId { get; set; }

    public long ChecklistItemId { get; set; }

    public string Outcome { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime? InspectedAt { get; set; }

    public virtual InspectionChecklistItem ChecklistItem { get; set; } = null!;

    public virtual RoomInspection Inspection { get; set; } = null!;
}
