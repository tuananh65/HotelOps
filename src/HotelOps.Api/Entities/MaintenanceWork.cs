using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaintenanceWork
{
    public long MaintenanceWorkId { get; set; }

    public string WorkNo { get; set; } = null!;

    public long? IncidentId { get; set; }

    public long? AssigneeId { get; set; }

    public long? DepartmentId { get; set; }

    public string Title { get; set; } = null!;

    public string? Instruction { get; set; }

    public long? PriorityId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PlannedStartAt { get; set; }

    public DateTime? PlannedEndAt { get; set; }

    public DateTime? ActualStartAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolutionNote { get; set; }

    public long? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? Assignee { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual Department? Department { get; set; }

    public virtual Incident? Incident { get; set; }

    public virtual ICollection<MaintenanceWorkLog> MaintenanceWorkLogs { get; set; } = new List<MaintenanceWorkLog>();

    public virtual ICollection<PreventiveMaintenanceRun> PreventiveMaintenanceRuns { get; set; } = new List<PreventiveMaintenanceRun>();

    public virtual ReferenceValue? Priority { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }
}
