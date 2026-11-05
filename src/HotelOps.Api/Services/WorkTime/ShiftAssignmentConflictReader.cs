using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

/// <summary>
/// Trusted backend read helper for UC138, not an authorization or concurrency boundary.
/// A future writer must validate actor/employee eligibility, acquire sorted User locks and
/// retained SQL range/source locks, then repeat this check in its audit/outbox transaction.
/// No route/DI registration or shift mutation is activated by this component.
/// </summary>
public sealed class ShiftAssignmentConflictReader(HotelOpsDbContext db)
{
    public async Task<bool> HasOverlapAsync(long employeeId, DateTimeOffset startUtc,
        DateTimeOffset endUtc, long? excludeAssignmentId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (employeeId <= 0 || excludeAssignmentId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(employeeId));
        var candidate = new RecordedWorkRange(startUtc, endUtc);
        WorkIntervalMath.Validate(candidate);
        if (startUtc == endUtc) throw new ArgumentException("A scheduled shift must have positive duration.");
        var start = startUtc.UtcDateTime;
        var end = endUtc.UtcDateTime;
        // Global employee scope: another branch's assignment also consumes the person's time.
        // Only explicit Cancelled is ignored. Unknown legacy states are never assumed free.
        var rows = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.UserId == employeeId && a.AssignmentStatus != "CANCELLED"
                && (excludeAssignmentId == null || a.ShiftAssignmentId != excludeAssignmentId)
                && ((a.Shift.ScheduledStartAt < end && a.Shift.ScheduledEndAt > start)
                    || a.Shift.ScheduledEndAt <= a.Shift.ScheduledStartAt))
            .Select(a => new { a.Shift.ScheduledStartAt, a.Shift.ScheduledEndAt }).ToArrayAsync(ct);
        foreach (var row in rows)
        {
            if (row.ScheduledEndAt <= row.ScheduledStartAt)
                throw new InvalidOperationException("Stored shift interval is unresolved; cannot certify non-overlap.");
            var existing = new RecordedWorkRange(Utc(row.ScheduledStartAt), Utc(row.ScheduledEndAt));
            if (WorkIntervalMath.Overlaps(candidate, existing)) return true;
        }
        return false;
    }

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
