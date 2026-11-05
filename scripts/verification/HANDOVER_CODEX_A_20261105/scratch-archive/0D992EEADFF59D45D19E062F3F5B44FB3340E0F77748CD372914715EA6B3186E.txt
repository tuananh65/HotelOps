using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialRequestApproval
{
    public long ApprovalId { get; set; }

    public long RequestId { get; set; }

    public int ApprovalLevel { get; set; }

    public long ApproverId { get; set; }

    public string Decision { get; set; } = null!;

    public string? DecisionNote { get; set; }

    public DateTime DecidedAt { get; set; }

    public virtual User Approver { get; set; } = null!;

    public virtual MaterialRequest Request { get; set; } = null!;

    public long? ApprovalCycleId { get; set; }

    public virtual MaterialRequestApprovalCycle? ApprovalCycle { get; set; }
}
