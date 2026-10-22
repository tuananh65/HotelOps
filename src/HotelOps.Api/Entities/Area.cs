using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Area
{
    public long AreaId { get; set; }

    public long BranchId { get; set; }

    public long? FloorId { get; set; }

    public string AreaCode { get; set; } = null!;

    public string AreaName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual Branch Branch { get; set; } = null!;

    public virtual Floor? Floor { get; set; }

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public virtual ICollection<InternalRequest> InternalRequests { get; set; } = new List<InternalRequest>();

    public virtual ICollection<OperationalTask> OperationalTasks { get; set; } = new List<OperationalTask>();

    public virtual ICollection<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules { get; set; } = new List<PreventiveMaintenanceSchedule>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();
}
