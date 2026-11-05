using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class WorkCategory
{
    public long CategoryId { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string CategoryName { get; set; } = null!;

    public long? DepartmentId { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public virtual Department? Department { get; set; }

    public virtual ICollection<OperationalTask> OperationalTasks { get; set; } = new List<OperationalTask>();
}
