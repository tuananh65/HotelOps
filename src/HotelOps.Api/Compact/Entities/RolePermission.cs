using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class RolePermission
{
    public long GrantId { get; set; }

    public long RoleId { get; set; }

    public long PermissionId { get; set; }

    public string DataScope { get; set; } = null!;

    public byte[] RowVersion { get; set; } = null!;


}
