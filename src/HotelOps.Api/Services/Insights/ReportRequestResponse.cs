namespace HotelOps.Api.Services.Insights;

// RPT-07 uses the same SubmittedAt -> ACCEPT/REJECT clock as UC116.
// A mutable submission timestamp cannot prove the origin of a reopened cycle.
internal static class ReportRequestResponse
{
    internal sealed record Event(long Id, string Type, string? OldStatus, string? NewStatus, DateTime At, bool AssignmentVerified = false);
    internal sealed record Result(long? ResponseEventId, DateTime? ResponseAt, decimal? ResponseSeconds, string ResponseEvidence);

    internal static Result Read(DateTime createdAt, DateTime? submittedAt, DateTime now, IReadOnlyList<Event> events)
    {
        Result Missing(string reason) => new(null, null, null, reason);
        if (events.Any(e => e.Type == "REOPEN" || e.OldStatus == "CLOSED" && e.NewStatus != "CLOSED"))
            return Missing("CYCLE_ORIGIN_UNAVAILABLE");
        var decisions = events.Where(e => e.Type is "ACCEPT" or "REJECT").ToArray();
        if (decisions.Length == 0) return Missing("NO_RECORDED_DECISION");
        if (decisions.Length != 1) return Missing("AMBIGUOUS_DECISION_HISTORY");
        var decision = decisions[0];
        if (decision.OldStatus != "PENDING_ACCEPTANCE"
            || decision.NewStatus != (decision.Type == "ACCEPT" ? "ACCEPTED" : "REJECTED"))
            return Missing("INCONSISTENT_DECISION_TRANSITION");
        if (submittedAt is not {} submitted || submitted < createdAt || decision.At < submitted || decision.At > now)
            return Missing("INVALID_RESPONSE_TIMESTAMPS");
        return new(decision.Id, decision.At, (decimal)(decision.At.Ticks - submitted.Ticks) / TimeSpan.TicksPerSecond,
            "RECORDED_DECISION");
    }
}
