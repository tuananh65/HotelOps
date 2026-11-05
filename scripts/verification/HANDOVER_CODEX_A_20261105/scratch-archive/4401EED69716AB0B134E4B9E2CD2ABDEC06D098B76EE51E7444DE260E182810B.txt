using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class UserBranchAccess
{
    public long AccessId { get; set; }

    public long UserId { get; set; }

    public long BranchId { get; set; }

    public string AccessLevel { get; set; } = null!;

    public bool IsPrimary { get; set; }

    public long? GrantedBy { get; set; }

    public DateTime GrantedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual User? GrantedByNavigation { get; set; }

    public virtual User User { get; set; } = null!;
}
