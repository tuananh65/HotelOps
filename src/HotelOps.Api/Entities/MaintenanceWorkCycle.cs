using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaintenanceWorkCycle
{
    public long CycleId { get; set; }

    public long MaintenanceWorkId { get; set; }

    public int CycleNo { get; set; }

    public bool IsLegacySnapshot { get; set; }

    public DateTime RecordedAt { get; set; }

    public long RecordedBy { get; set; }

    public DateTime? OpenedAt { get; set; }

    public long? OpenedBy { get; set; }

    public string? ReopenReason { get; set; }

    public string? StartingWorkStatus { get; set; }

    public string? StartingIncidentStatus { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public string? TerminalStatus { get; set; }

    public DateTime? EndedAt { get; set; }

    public long? EndedBy { get; set; }

    public string? TerminalReason { get; set; }

    public long? AssigneeId { get; set; }

    public long? DepartmentId { get; set; }

    public long? PriorityId { get; set; }

    public string? Title { get; set; }

    public string? Instruction { get; set; }

    public DateTime? PlannedStartAt { get; set; }

    public DateTime? PlannedEndAt { get; set; }

    public DateTime? ActualStartAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolutionNote { get; set; }

    public long? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? IncidentResolvedAt { get; set; }

    public DateTime? IncidentClosedAt { get; set; }

    public string? IncidentClosureNote { get; set; }

    public virtual User? Assignee { get; set; }

    public virtual Department? Department { get; set; }

    public virtual User? EndedByNavigation { get; set; }

    public virtual MaintenanceWork MaintenanceWork { get; set; } = null!;

    public virtual ICollection<MaintenanceWorkLog> MaintenanceWorkLogs { get; set; } = new List<MaintenanceWorkLog>();

    public virtual User? OpenedByNavigation { get; set; }

    public virtual ReferenceValue? Priority { get; set; }

    public virtual User RecordedByNavigation { get; set; } = null!;

    public virtual User? ReviewedByNavigation { get; set; }
}
