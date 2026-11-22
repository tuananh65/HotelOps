using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class UserRole
{
    public long UserRoleId { get; set; }

    public long UserId { get; set; }

    public long RoleId { get; set; }

    public long? BranchId { get; set; }

    public long? AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;




}
