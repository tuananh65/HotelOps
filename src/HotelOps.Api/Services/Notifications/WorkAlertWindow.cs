namespace HotelOps.Api.Services.Notifications;

internal enum WorkAlertCondition { Clear, AtRisk, Overdue, NotEvaluable, Unknown }

internal sealed record WorkAlertObservation(long? EpisodeId, WorkAlertCondition Condition);

internal static class WorkAlertWindow
{
    // A missing timestamp is not evidence that an existing violation ended.
    // Unknown must retain the episode until its owning workflow can be read.
    internal static WorkAlertCondition Evaluate(bool terminal, DateTime? dueAt, DateTime? allowedFrom,
        DateTime now, decimal thresholdPercent)
    {
        if (thresholdPercent is <= 0 or > 100) throw new ArgumentOutOfRangeException(nameof(thresholdPercent));
        if (terminal) return WorkAlertCondition.Clear;
        if (dueAt is not { } due) return WorkAlertCondition.NotEvaluable;
        if (now > due) return WorkAlertCondition.Overdue;
        if (allowedFrom is { } invalid && due <= invalid) return WorkAlertCondition.NotEvaluable;
        if (allowedFrom is not { } start) return WorkAlertCondition.NotEvaluable;
        if (now < start) return WorkAlertCondition.Clear;
        // Decimal tick arithmetic avoids rounded-second boundary errors and
        // preserves the approved inclusive 80% and exclusive overdue boundary.
        return (decimal)(now.Ticks - start.Ticks) * 100 >= (decimal)(due.Ticks - start.Ticks) * thresholdPercent
            ? WorkAlertCondition.AtRisk : WorkAlertCondition.Clear;
    }
}
