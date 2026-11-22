using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class Membership
{
    public long MembershipId { get; set; }

    public long BranchId { get; set; }

    public long UserId { get; set; }

    public long? DepartmentId { get; set; }

    public string Kind { get; set; } = null!;

    public string? AccessLevel { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public long? RecordedBy { get; set; }

    public string? Provenance { get; set; }

    public byte[] RowVersion { get; set; } = null!;




}
