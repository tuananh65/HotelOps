namespace HotelOps.Api.Entities;

public partial class WorkInterval
{
    public long IntervalId { get; set; }
    public long WorkdayId { get; set; }
    public long UserId { get; set; }
    public long BranchId { get; set; }
    public string WorkItemKind { get; set; } = null!;
    public long WorkItemId { get; set; }
    public long OpenedBy { get; set; }
    public DateTime OpenedAt { get; set; }
    public long? ClosedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime EffectiveStartedAt { get; set; }
    public DateTime? EffectiveEndedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
