using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InspectionDefect
{
    public long DefectId { get; set; }

    public long InspectionId { get; set; }

    public long? ChecklistItemId { get; set; }

    public long? SeverityId { get; set; }

    public string Description { get; set; } = null!;

    public string? CorrectiveAction { get; set; }

    public string Status { get; set; } = null!;

    public long? AssignedTo { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolutionNote { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual InspectionChecklistItem? ChecklistItem { get; set; }

    public virtual RoomInspection Inspection { get; set; } = null!;

    public virtual ReferenceValue? Severity { get; set; }
}
