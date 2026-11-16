namespace HotelOps.Api.Services.Insights;

// KPI-05 clarification approved by DEV3: elapsed processing cycles, not work effort.
internal static class TaskCycleDuration
{
    internal sealed record Acceptance(long SubmissionId, long IntervalId, DateTime SubmittedAt, DateTime AcceptedAt);
    internal sealed record Reopen(long PreviousSubmissionId, long IntervalCutoff, DateTime ReopenedAt);
    internal sealed record Interval(long Id, DateTime StartedAt, DateTime? ClosedAt, bool HasStartReceipt);

    internal static decimal? Seconds(DateTime completedAt, IReadOnlyList<Acceptance> accepted,
        IReadOnlyList<Reopen> reopens, IReadOnlyList<Interval> intervals)
    {
        var cycles = accepted.OrderBy(a => a.AcceptedAt).ToArray();
        var boundaries = reopens.OrderBy(r => r.ReopenedAt).ToArray();
        if (cycles.Length == 0 || cycles.Length != boundaries.Length + 1
            || cycles.Select(c => c.SubmissionId).Distinct().Count() != cycles.Length
            || cycles[^1].AcceptedAt != completedAt || intervals.Count == 0
            || intervals.Select(i => i.Id).Distinct().Count() != intervals.Count) return null;
        long cutoff = 0;
        DateTime? lower = null;
        decimal total = 0;
        for (var index = 0; index < cycles.Length; index++)
        {
            var cycle = cycles[index];
            var upper = index < boundaries.Length ? boundaries[index].IntervalCutoff : long.MaxValue;
            var work = intervals.Where(i => i.Id > cutoff && i.Id <= upper).OrderBy(i => i.Id).ToArray();
            if (upper <= cutoff || work.Length == 0 || !work[0].HasStartReceipt
                || work[0].StartedAt < lower || cycle.SubmittedAt < work[0].StartedAt
                || cycle.AcceptedAt < cycle.SubmittedAt
                || work.Any(i => i.StartedAt < work[0].StartedAt || i.ClosedAt == null
                    || i.ClosedAt < i.StartedAt || i.ClosedAt > cycle.SubmittedAt)
                || work[^1].Id != cycle.IntervalId || work[^1].ClosedAt != cycle.SubmittedAt) return null;
            total += (cycle.SubmittedAt.Ticks - work[0].StartedAt.Ticks) / (decimal)TimeSpan.TicksPerSecond;
            if (index < boundaries.Length)
            {
                var boundary = boundaries[index];
                if (boundary.PreviousSubmissionId != cycle.SubmissionId || boundary.IntervalCutoff != work[^1].Id
                    || boundary.ReopenedAt < cycle.AcceptedAt) return null;
                cutoff = boundary.IntervalCutoff;
                lower = boundary.ReopenedAt;
            }
        }
        return total;
    }
}
