using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaterialRequest
{
    public long RequestId { get; set; }

    public string RequestNo { get; set; } = null!;

    public long BranchId { get; set; }

    public long DepartmentId { get; set; }

    public long RequesterId { get; set; }

    public long? SourceWarehouseId { get; set; }

    public string Purpose { get; set; } = null!;

    public DateTime? NeededAt { get; set; }

    public long? PriorityId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? SubmittedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<MaterialFulfillment> MaterialFulfillments { get; set; } = new List<MaterialFulfillment>();

    public virtual ICollection<MaterialRequestApproval> MaterialRequestApprovals { get; set; } = new List<MaterialRequestApproval>();

    public virtual ICollection<MaterialRequestLine> MaterialRequestLines { get; set; } = new List<MaterialRequestLine>();

    public virtual ReferenceValue? Priority { get; set; }

    public virtual User Requester { get; set; } = null!;

    public virtual Warehouse? SourceWarehouse { get; set; }
}
