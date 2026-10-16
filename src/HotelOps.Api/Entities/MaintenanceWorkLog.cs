using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class MaintenanceWorkLog
{
    public long LogId { get; set; }

    public long MaintenanceWorkId { get; set; }

    public string WorkNote { get; set; } = null!;

    public int SpentMinutes { get; set; }

    public long? MaterialIssueId { get; set; }

    public string? Status { get; set; }

    public long LoggedBy { get; set; }

    public DateTime LoggedAt { get; set; }

    public virtual User LoggedByNavigation { get; set; } = null!;

    public virtual MaintenanceWork MaintenanceWork { get; set; } = null!;

    public virtual StockDocument? MaterialIssue { get; set; }
}
