using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Room
{
    public long RoomId { get; set; }

    public long BranchId { get; set; }

    public long FloorId { get; set; }

    public long? AreaId { get; set; }

    public long RoomTypeId { get; set; }

    public string RoomNumber { get; set; } = null!;

    public string OperationalStatus { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Area? Area { get; set; }

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<CleaningTask> CleaningTasks { get; set; } = new List<CleaningTask>();

    public virtual Floor Floor { get; set; } = null!;

    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public virtual ICollection<InternalRequest> InternalRequests { get; set; } = new List<InternalRequest>();

    public virtual ICollection<OperationalTask> OperationalTasks { get; set; } = new List<OperationalTask>();

    public virtual ICollection<PreventiveMaintenanceSchedule> PreventiveMaintenanceSchedules { get; set; } = new List<PreventiveMaintenanceSchedule>();

    public virtual ICollection<RoomBlock> RoomBlocks { get; set; } = new List<RoomBlock>();

    public virtual ICollection<RoomInspection> RoomInspections { get; set; } = new List<RoomInspection>();

    public virtual ICollection<RoomStatusHistory> RoomStatusHistories { get; set; } = new List<RoomStatusHistory>();

    public virtual RoomType RoomType { get; set; } = null!;

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();
}
