using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Floor
{
    public long FloorId { get; set; }

    public long BranchId { get; set; }

    public string FloorCode { get; set; } = null!;

    public string FloorName { get; set; } = null!;

    public int? FloorNumber { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Area> Areas { get; set; } = new List<Area>();

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
