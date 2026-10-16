using System;
using System.Collections.Generic;

namespace HotelOps.Api.Entities;

public partial class Incident
{
    public long IncidentId { get; set; }

    public string IncidentNo { get; set; } = null!;

    public long BranchId { get; set; }

    public long? RoomId { get; set; }

    public long? AreaId { get; set; }

    public long? AssetId { get; set; }

    public string IncidentTitle { get; set; } = null!;

    public string Description { get; set; } = null!;

    public long? SeverityId { get; set; }

    public long? PriorityId { get; set; }

    public string Status { get; set; } = null!;

    public long ReportedBy { get; set; }

    public DateTime DiscoveredAt { get; set; }

    public DateTime ReportedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? ClosureNote { get; set; }

    public virtual Area? Area { get; set; }

    public virtual Asset? Asset { get; set; }

    public virtual ICollection<AssetConditionHistory> AssetConditionHistories { get; set; } = new List<AssetConditionHistory>();

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<IncidentEvent> IncidentEvents { get; set; } = new List<IncidentEvent>();

    public virtual ICollection<InternalRequest> InternalRequests { get; set; } = new List<InternalRequest>();

    public virtual ICollection<MaintenanceWork> MaintenanceWorks { get; set; } = new List<MaintenanceWork>();

    public virtual ReferenceValue? Priority { get; set; }

    public virtual User ReportedByNavigation { get; set; } = null!;

    public virtual Room? Room { get; set; }

    public virtual ICollection<RoomBlock> RoomBlocks { get; set; } = new List<RoomBlock>();

    public virtual ReferenceValue? Severity { get; set; }
}
