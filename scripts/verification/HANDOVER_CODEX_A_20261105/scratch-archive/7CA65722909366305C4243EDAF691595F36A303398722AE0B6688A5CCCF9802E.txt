using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class CleaningRework
{
    public long ReworkId { get; set; }

    public long CleaningTaskId { get; set; }

    public long InspectionId { get; set; }

    public long? AssigneeUserId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime RequestedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Note { get; set; }

    public virtual User? AssigneeUser { get; set; }

    public virtual CleaningTask CleaningTask { get; set; } = null!;

    public virtual RoomInspection Inspection { get; set; } = null!;
}
