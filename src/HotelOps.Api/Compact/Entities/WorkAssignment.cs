using System;
using System.Collections.Generic;

namespace HotelOps.Api.Compact.Entities;

public partial class WorkAssignment
{
    public long AssignmentId { get; set; }

    public long BranchId { get; set; }

    public long WorkId { get; set; }

    public long AssigneeUserId { get; set; }

    public long AssignedBy { get; set; }

    public string AssignmentStatus { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? Reason { get; set; }

    public byte[] RowVersion { get; set; } = null!;





}
