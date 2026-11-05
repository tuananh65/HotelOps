using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InspectionChecklist
{
    public long ChecklistId { get; set; }

    public string ChecklistCode { get; set; } = null!;

    public string ChecklistName { get; set; } = null!;

    public long? RoomTypeId { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<InspectionChecklistVersion> InspectionChecklistVersions { get; set; } = new List<InspectionChecklistVersion>();

    public virtual RoomType? RoomType { get; set; }
}
