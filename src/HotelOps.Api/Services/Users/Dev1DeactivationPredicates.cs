using System.Linq.Expressions;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

/// <summary>
/// Positive evidence only, derived from the existing cleaning execution ownership contract.
/// This is not a complete DEV1 eligibility gate. A negative result leaves other cleaning
/// states, inconsistent legacy data and all other DEV1 responsibilities unresolved.
/// Uses the caller's DbContext/transaction; does not acquire locks or perform mutations.
/// </summary>
internal static class Dev1DeactivationPredicates
{
    internal static Expression<Func<CleaningAssignment, bool>> KnownExecutingCleaning(long targetId, DateTime nowUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetId);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC evaluation time is required.", nameof(nowUtc));
        // Match CleaningTaskResourceResolver's proven ownership subset. Do not broaden
        // these states or treat its permission-denial cases as absence of responsibility.
        return assignment => assignment.AssigneeUserId == targetId
            && assignment.AssignmentStatus == "ACTIVE" && assignment.EndedAt == null && assignment.AssignedAt <= nowUtc
            && assignment.CleaningTask.Status == "IN_PROGRESS"
            && assignment.CleaningTask.Room.IsActive && assignment.CleaningTask.Room.BranchId > 0
            && assignment.CleaningTask.CleaningAssignments.Count(other => other.AssignmentStatus == "ACTIVE"
                && other.EndedAt == null && other.AssignedAt <= nowUtc) == 1;
    }

    internal static Task<bool> HasKnownExecutingCleaningAsync(HotelOpsDbContext db, long targetId,
        DateTime nowUtc, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return db.CleaningAssignments.AsNoTracking().AnyAsync(KnownExecutingCleaning(targetId, nowUtc), ct);
    }
}
