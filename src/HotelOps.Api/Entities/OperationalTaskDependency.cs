using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class OperationalTaskDependency
{
    public long DependencyId { get; set; }

    public long PredecessorTaskId { get; set; }

    public long SuccessorTaskId { get; set; }

    public string DependencyType { get; set; } = null!;

    public int? LagMinutes { get; set; }

    public virtual OperationalTask PredecessorTask { get; set; } = null!;

    public virtual OperationalTask SuccessorTask { get; set; } = null!;
}
