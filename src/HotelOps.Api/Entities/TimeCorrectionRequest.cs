namespace HotelOps.Api.Entities;

public partial class TimeCorrectionRequest
{
    public long CorrectionId { get; set; }
    public long IntervalId { get; set; }
    public long RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime OriginalStartedAt { get; set; }
    public DateTime OriginalEndedAt { get; set; }
    public byte[] OriginalIntervalVersion { get; set; } = null!;
    public DateTime ProposedStartedAt { get; set; }
    public DateTime ProposedEndedAt { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = null!;
    public long? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? AppliedStartedAt { get; set; }
    public DateTime? AppliedEndedAt { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
