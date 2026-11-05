using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class RoomInspection
{
    public long InspectionId { get; set; }

    public string InspectionNo { get; set; } = null!;

    public long RoomId { get; set; }

    public long? CleaningTaskId { get; set; }

    public long VersionId { get; set; }

    public long InspectorUserId { get; set; }

    public string Status { get; set; } = null!;

    public string? Result { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public DateTime? InspectedAt { get; set; }

    public string? SummaryNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CleaningRework> CleaningReworks { get; set; } = new List<CleaningRework>();

    public virtual CleaningTask? CleaningTask { get; set; }

    public virtual ICollection<InspectionDefect> InspectionDefects { get; set; } = new List<InspectionDefect>();

    public virtual ICollection<InspectionResult> InspectionResults { get; set; } = new List<InspectionResult>();

    public virtual User InspectorUser { get; set; } = null!;

    public virtual Room Room { get; set; } = null!;

    public virtual InspectionChecklistVersion Version { get; set; } = null!;
}
