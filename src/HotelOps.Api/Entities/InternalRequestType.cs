using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class InternalRequestType
{
    public long RequestTypeId { get; set; }

    public string TypeCode { get; set; } = null!;

    public string TypeName { get; set; } = null!;

    public long? DefaultDepartmentId { get; set; }

    public long? DefaultPriorityId { get; set; }

    public int? SlaMinutes { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual Department? DefaultDepartment { get; set; }

    public virtual ReferenceValue? DefaultPriority { get; set; }

    public virtual ICollection<InternalRequest> InternalRequests { get; set; } = new List<InternalRequest>();
}
