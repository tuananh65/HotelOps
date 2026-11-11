using HotelOps.Api.Entities;
namespace HotelOps.Api.Services.WorkTime;

/// <summary>One scheduled assignment can be claimed once, including an ended workday.
/// Caller retains authorization and User-first transaction locks. No state/history is deleted.</summary>
public static class ScheduledWorkdayClaims
{
    public static IQueryable<ShiftAssignment> Unclaimed(IQueryable<ShiftAssignment> assignments,
        IQueryable<Workday> workdays) => assignments.Where(a => !workdays.Any(d => d.ShiftAssignmentId == a.ShiftAssignmentId));
}
