using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Role
{
    public long RoleId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public byte[] RowVersion { get; set; } = null!;


}
