namespace HotelOps.Api.Compact.Contracts;

public sealed record AssignShiftsRequest(long ShiftId, long[] UserIds, DateOnly[] WorkDates,
    string RowVersion, Guid OperationKey, bool HolidaysAcknowledged = false,
    Dictionary<long, string>? HoursAcknowledgements = null);
public sealed record SaveWorkdayShiftRequest(long BranchId, long DepartmentId, long RoleId,
    string Name, int StartMinute, int EndMinute, int Capacity, bool IsActive,
    string? RowVersion, Guid OperationKey);
public sealed record WorkdayCandidateDto(long UserId, string EmployeeCode, string FullName,
    WorkdayHourWarning[]? HourWarnings = null, string? HoursToken = null);
public sealed record WorkdayHourWarning(string Period, DateOnly From, DateOnly To,
    int ExistingMinutes, int AddedMinutes, int LimitMinutes)
{
    public int TotalMinutes => ExistingMinutes + AddedMinutes;
}
public sealed record WorkdayHoursReview(WorkdayHourWarning[] Warnings, string Token);
public sealed record WorkdaySlotDto(DateOnly WorkDate, int Remaining);
public sealed record WorkdayCandidatesDto(WorkdayCandidateDto[] Users, WorkdaySlotDto[] Slots);
public sealed record WorkdayCalendarMarker(DateOnly Date, string Name, string Kind, string Source);
