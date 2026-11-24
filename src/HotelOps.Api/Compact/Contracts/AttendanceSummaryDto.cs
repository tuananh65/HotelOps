namespace HotelOps.Api.Compact.Contracts;

public sealed record AttendanceEntry(DateOnly WorkDate, string Status, DateTime? StartedAt, DateTime? EndedAt);
public sealed record AttendancePeriod(string Period, DateOnly From, DateOnly To, int CompletedShifts,
    long WorkedSeconds, decimal WorkedHours, int WorkingShifts);
public sealed record AttendanceSummaryDto(long BranchId, string Timezone, DateTime UpdatedAt, AttendancePeriod[] Periods);
