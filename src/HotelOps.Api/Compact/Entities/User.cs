using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class User
{
    public long UserId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string? Email { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? PasswordHash { get; set; }

    public Guid SecurityStamp { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? TemporaryPasswordExpiresAt { get; set; }

    public long? ManagerUserId { get; set; }

    public string EmploymentStatus { get; set; } = null!;

    public string AccountStatus { get; set; } = null!;

    public DateTime? LastLoginAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;





































}
