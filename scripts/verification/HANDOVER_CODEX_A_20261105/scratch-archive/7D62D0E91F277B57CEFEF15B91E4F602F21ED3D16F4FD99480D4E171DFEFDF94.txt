using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class CleaningTask
{
    public long CleaningTaskId { get; set; }

    public string TaskNo { get; set; } = null!;

    public long RoomId { get; set; }

    public long CleaningTypeId { get; set; }

    public long? PriorityId { get; set; }

    public string? Instruction { get; set; }

    public DateTime? ScheduledStartAt { get; set; }

    public DateTime? DueAt { get; set; }

    public DateTime? ActualStartAt { get; set; }

    public DateTime? ActualEndAt { get; set; }

    public string Status { get; set; } = null!;

    public string? CompletionNote { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CleaningAssignment> CleaningAssignments { get; set; } = new List<CleaningAssignment>();

    public virtual ICollection<CleaningProgressLog> CleaningProgressLogs { get; set; } = new List<CleaningProgressLog>();

    public virtual ICollection<CleaningRework> CleaningReworks { get; set; } = new List<CleaningRework>();

    public virtual ReferenceValue CleaningType { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ReferenceValue? Priority { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual ICollection<RoomInspection> RoomInspections { get; set; } = new List<RoomInspection>();
}
