namespace HotelOps.Api.Services.Insights;

// Approved RPT-07: first assignment into IN_PROGRESS, then explicit reopen cycles.
// Reassigning an in-progress request never resets its processing clock.
internal static class ReportRequestProcessing
{
    internal sealed record Result(decimal? CompletionSeconds, int? CompletedCycles, int? ReopenCount, string CompletionEvidence);

    internal static Result Read(string status, DateTime createdAt, DateTime? completedAt, DateTime? closedAt,
        DateTime now, IReadOnlyList<ReportRequestResponse.Event> history)
    {
        Result Missing(string reason) => new(null, null, null, reason);
        var events = history.Where(e => e.Type is "ASSIGN" or "REASSIGN" or "REOPEN" or "COMPLETE"
            || e.OldStatus == "CLOSED" && e.NewStatus != "CLOSED").OrderBy(e => e.At).ThenBy(e => e.Id).ToArray();
        if (events.Length == 0) return Missing("NO_PROCESSING_HISTORY");
        if (events.Any(e => e.At < createdAt || e.At > now) || events.Select(e => e.Id).Distinct().Count() != events.Length)
            return Missing("INVALID_PROCESSING_TIMESTAMPS");
        DateTime? start = null, lastClosed = null;
        decimal seconds = 0;
        var cycles = 0;
        var reopens = 0;
        foreach (var e in events)
        {
            if (e.Type == "ASSIGN" && e.OldStatus == "ACCEPTED" && e.NewStatus == "IN_PROGRESS"
                && e.AssignmentVerified && start == null && cycles == 0)
                start = e.At;
            else if (e.Type == "REASSIGN" && e.OldStatus == "IN_PROGRESS" && e.NewStatus == "IN_PROGRESS"
                && e.AssignmentVerified && start != null) { }
            else if (e.Type == "COMPLETE" && e.OldStatus == "IN_PROGRESS" && e.NewStatus == "CLOSED" && start is {} origin)
            {
                seconds += (decimal)(e.At.Ticks - origin.Ticks) / TimeSpan.TicksPerSecond;
                start = null;
                lastClosed = e.At;
                cycles++;
            }
            else if (e.Type == "REOPEN" && e.OldStatus == "CLOSED" && e.NewStatus == "IN_PROGRESS"
                && start == null && lastClosed != null)
            {
                start = e.At;
                reopens++;
            }
            else return Missing("INCONSISTENT_PROCESSING_HISTORY");
        }
        if (start != null)
            return status == "IN_PROGRESS" && completedAt == null && closedAt == null
                ? new(null, cycles, reopens, "OPEN_PROCESSING_CYCLE") : Missing("INCONSISTENT_CURRENT_CLOSURE");
        if (status != "CLOSED" || cycles == 0 || completedAt != lastClosed || closedAt != lastClosed)
            return Missing("INCONSISTENT_CURRENT_CLOSURE");
        return new(seconds, cycles, reopens, "RECORDED_PROCESSING_CYCLES");
    }
}
