using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InspectionChecklistVersion
{
    public long VersionId { get; set; }

    public long ChecklistId { get; set; }

    public int VersionNo { get; set; }

    public DateTime? EffectiveAt { get; set; }

    public string Status { get; set; } = null!;

    public long? PublishedBy { get; set; }

    public virtual InspectionChecklist Checklist { get; set; } = null!;

    public virtual ICollection<InspectionChecklistItem> InspectionChecklistItems { get; set; } = new List<InspectionChecklistItem>();

    public virtual User? PublishedByNavigation { get; set; }

    public virtual ICollection<RoomInspection> RoomInspections { get; set; } = new List<RoomInspection>();
}
