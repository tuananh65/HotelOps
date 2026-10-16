using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class ReferenceValue
{
    public long ReferenceId { get; set; }

    public string GroupCode { get; set; } = null!;

    public string ValueCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }

    public int? SortOrder { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<CleaningProgressLog> CleaningProgressLogs { get; set; } = new List<CleaningProgressLog>();

    public virtual ICollection<CleaningTask> CleaningTaskCleaningTypes { get; set; } = new List<CleaningTask>();

    public virtual ICollection<CleaningTask> CleaningTaskPriorities { get; set; } = new List<CleaningTask>();

    public virtual ICollection<Incident> IncidentPriorities { get; set; } = new List<Incident>();

    public virtual ICollection<Incident> IncidentSeverities { get; set; } = new List<Incident>();

    public virtual ICollection<InspectionDefect> InspectionDefects { get; set; } = new List<InspectionDefect>();

    public virtual ICollection<InternalRequestType> InternalRequestTypes { get; set; } = new List<InternalRequestType>();

    public virtual ICollection<InternalRequest> InternalRequests { get; set; } = new List<InternalRequest>();

    public virtual ICollection<MaintenanceWork> MaintenanceWorks { get; set; } = new List<MaintenanceWork>();

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<OperationalTask> OperationalTasks { get; set; } = new List<OperationalTask>();
}
