using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class UserSession
{
    public long SessionId { get; set; }

    public long UserId { get; set; }

    public string RefreshTokenHash { get; set; } = null!;

    public string? DeviceInfo { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
