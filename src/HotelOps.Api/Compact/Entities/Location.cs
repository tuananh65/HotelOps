using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Location
{
    public long LocationId { get; set; }

    public long BranchId { get; set; }

    public long? ParentLocationId { get; set; }

    public string Kind { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int? FloorNumber { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;










}
