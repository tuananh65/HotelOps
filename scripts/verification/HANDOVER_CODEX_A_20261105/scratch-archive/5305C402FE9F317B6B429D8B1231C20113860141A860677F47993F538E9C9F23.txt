using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class OperationalTask
{
    public long TaskId { get; set; }

    public string TaskCode { get; set; } = null!;

    public long BranchId { get; set; }

    public long DepartmentId { get; set; }

    public long? CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public long? PriorityId { get; set; }

    public long? RoomId { get; set; }

    public long? AreaId { get; set; }

    public long? ParentTaskId { get; set; }

    public string? SourceType { get; set; }

    public long? SourceId { get; set; }

    public string Status { get; set; } = null!;

    public int ProgressPercent { get; set; }

    public DateTime? PlannedStartAt { get; set; }

    public DateTime? DueAt { get; set; }

    public DateTime? ActualStartAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? CompletionNote { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Area? Area { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual WorkCategory? Category { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<OperationalTask> InverseParentTask { get; set; } = new List<OperationalTask>();

    public virtual ICollection<OperationalTaskAssignment> OperationalTaskAssignments { get; set; } = new List<OperationalTaskAssignment>();

    public virtual ICollection<OperationalTaskDependency> OperationalTaskDependencyPredecessorTasks { get; set; } = new List<OperationalTaskDependency>();

    public virtual ICollection<OperationalTaskDependency> OperationalTaskDependencySuccessorTasks { get; set; } = new List<OperationalTaskDependency>();

    public virtual ICollection<OperationalTaskProgress> OperationalTaskProgresses { get; set; } = new List<OperationalTaskProgress>();

    public virtual OperationalTask? ParentTask { get; set; }

    public virtual ReferenceValue? Priority { get; set; }

    public virtual Room? Room { get; set; }
}
