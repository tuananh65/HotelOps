using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class RoomType
{
    public long RoomTypeId { get; set; }

    public long BranchId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int Capacity { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;



}
