using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class UserSession
{
    public long SessionId { get; set; }

    public long UserId { get; set; }

    public byte[] TokenHash { get; set; } = null!;

    public Guid SecurityStamp { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

}
