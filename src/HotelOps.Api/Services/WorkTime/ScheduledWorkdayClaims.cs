using HotelOps.Api.Entities;
namespace HotelOps.Api.Services.WorkTime;

/// <summary>One scheduled assignment can be claimed once, including an ended workday.
/// Caller retains authorization and User-first transaction locks. No state/history is deleted.</summary>
public static class ScheduledWorkdayClaims
{
    private static readonly TimeZoneInfo HotelTimezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    // Re-evaluate against the same authoritative SQL timestamp that will be persisted.
    // A lock/read wait must not carry yesterday's assignment into today's Workday.
    public static void RequireCurrentHotelDate(DateOnly shiftDate, DateTime serverUtc)
    {
        if (serverUtc.Kind != DateTimeKind.Utc || shiftDate != DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(serverUtc, HotelTimezone)))
            throw new WorkTimeContractException("TIME_STATE_CONFLICT");
    }

    public static IQueryable<ShiftAssignment> Unclaimed(IQueryable<ShiftAssignment> assignments,
        IQueryable<Workday> workdays) => assignments.Where(a => !workdays.Any(d => d.ShiftAssignmentId == a.ShiftAssignmentId));
}
