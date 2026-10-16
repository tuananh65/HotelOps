using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class RoomType
{
    public long RoomTypeId { get; set; }

    public string TypeCode { get; set; } = null!;

    public string TypeName { get; set; } = null!;

    public string? Description { get; set; }

    public int? StandardCapacity { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<InspectionChecklist> InspectionChecklists { get; set; } = new List<InspectionChecklist>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
