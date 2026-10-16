using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class UserRole
{
    public long UserRoleId { get; set; }

    public long UserId { get; set; }

    public long RoleId { get; set; }

    public long? BranchId { get; set; }

    public long? AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool IsActive { get; set; }

    public virtual User? AssignedByNavigation { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual Role Role { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
