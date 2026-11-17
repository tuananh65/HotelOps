namespace HotelOps.Api.DTOs.WorkTime;

public sealed record RequestTimeCorrection(DateTimeOffset Start, DateTimeOffset End, string Reason);
public sealed record DecideTimeCorrection(string Decision, DateTimeOffset? Start, DateTimeOffset? End, string? Reason);
public sealed record TimeCorrectionView(long CorrectionId, long BranchId, long DepartmentId, long IntervalId,
    long EmployeeId, string Kind, long WorkItemId, string Status, DateTimeOffset RequestedAt,
    DateTimeOffset OriginalStart, DateTimeOffset OriginalEnd, DateTimeOffset ProposedStart, DateTimeOffset ProposedEnd,
    string Reason, DateTimeOffset? AppliedStart, DateTimeOffset? AppliedEnd, long? DecidedBy,
    DateTimeOffset? DecidedAt, string? DecisionReason, string Version, bool CanDecide);
public sealed record TimeCorrectionOverlap(DateTimeOffset Start, DateTimeOffset? End);
public sealed record TimeCorrectionDetail(TimeCorrectionView Correction, TimeCorrectionOverlap[] OtherIntervals);
