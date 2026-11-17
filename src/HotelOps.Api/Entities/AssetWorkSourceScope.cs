namespace HotelOps.Api.Entities;

// Additive, immutable provenance recorded when an asset leaves a work's branch.
// Existing work identity and branch remain authoritative; this is not a grant.
public sealed class AssetWorkSourceScope
{
    public long ScopeId { get; set; }
    public long AssetId { get; set; }
    public long BranchId { get; set; }
    public long? IncidentId { get; set; }
    public long? RunId { get; set; }
    public long HandoverId { get; set; }
    public long AuditId { get; set; }
    public string AssetCode { get; set; } = "";
    public string AssetName { get; set; } = "";
    public DateTime CapturedAt { get; set; }
    public Incident? Incident { get; set; }
    public PreventiveMaintenanceRun? Run { get; set; }
}

public partial class Incident
{
    public virtual ICollection<AssetWorkSourceScope> AssetSourceScopes { get; set; } = new List<AssetWorkSourceScope>();
}

public partial class PreventiveMaintenanceRun
{
    public virtual ICollection<AssetWorkSourceScope> AssetSourceScopes { get; set; } = new List<AssetWorkSourceScope>();
}

public sealed class AssetScheduleSourceScope
{
    public long ScheduleId { get; set; }
    public long AssetId { get; set; }
    public long BranchId { get; set; }
    public long HandoverId { get; set; }
    public long AuditId { get; set; }
    public DateTime CapturedAt { get; set; }
}
