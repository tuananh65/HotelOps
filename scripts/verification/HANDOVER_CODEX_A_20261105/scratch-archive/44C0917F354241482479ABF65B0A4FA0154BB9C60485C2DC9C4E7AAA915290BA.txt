using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Permission
{
    public long PermissionId { get; set; }

    public string PermissionCode { get; set; } = null!;

    public string PermissionName { get; set; } = null!;

    public string ModuleCode { get; set; } = null!;

    public string ResourceCode { get; set; } = null!;

    public string ActionCode { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
