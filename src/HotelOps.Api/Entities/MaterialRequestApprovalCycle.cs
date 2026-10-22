using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialRequestApprovalCycle
{
    public long ApprovalCycleId { get; set; }

    public long RequestId { get; set; }

    public int RevisionNo { get; set; }

    public long SubmittedBy { get; set; }

    public long AssignedApproverId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime SubmittedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual User AssignedApprover { get; set; } = null!;

    public virtual MaterialRequestApproval? MaterialRequestApproval { get; set; }

    public virtual ICollection<MaterialRequestApprovalLineSnapshot> MaterialRequestApprovalLineSnapshots { get; set; } = new List<MaterialRequestApprovalLineSnapshot>();

    public virtual MaterialRequest Request { get; set; } = null!;

    public virtual User SubmittedByNavigation { get; set; } = null!;
}
