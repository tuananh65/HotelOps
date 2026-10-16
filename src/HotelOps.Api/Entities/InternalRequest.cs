using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InternalRequest
{
    public long InternalRequestId { get; set; }

    public string RequestNo { get; set; } = null!;

    public long BranchId { get; set; }

    public long RequestTypeId { get; set; }

    public long RequesterId { get; set; }

    public long? RequesterDepartmentId { get; set; }

    public long? ReceivingDepartmentId { get; set; }

    public long? RoomId { get; set; }

    public long? AreaId { get; set; }

    public long? RelatedIncidentId { get; set; }

    public string Subject { get; set; } = null!;

    public string Description { get; set; } = null!;

    public long? PriorityId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? RequiredAt { get; set; }

    public DateTime? DueAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Area? Area { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<InternalRequestAssignment> InternalRequestAssignments { get; set; } = new List<InternalRequestAssignment>();

    public virtual ICollection<InternalRequestEvent> InternalRequestEvents { get; set; } = new List<InternalRequestEvent>();

    public virtual InternalRequestFeedback? InternalRequestFeedback { get; set; }

    public virtual ReferenceValue? Priority { get; set; }

    public virtual Department? ReceivingDepartment { get; set; }

    public virtual Incident? RelatedIncident { get; set; }

    public virtual InternalRequestType RequestType { get; set; } = null!;

    public virtual User Requester { get; set; } = null!;

    public virtual Department? RequesterDepartment { get; set; }

    public virtual Room? Room { get; set; }
}
