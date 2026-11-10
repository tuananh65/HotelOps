namespace HotelOps.Api.Services.Housekeeping;

// Backend persistence contract. This is not an HTTP request or a Rework authority proof.
public sealed record CleanerAttribution(long CleaningTaskId, long RoomId, long BranchId,
    long AssignmentId, long CleanerUserId, long CompletionProgressId, DateTime CompletedAtUtc,
    byte[] CompletionRevision, long WriterTransactionId);

public static class CleanerAttributionContract
{
    public static bool UniqueCleaner(long cleaner, IEnumerable<long> assignmentUsers, IEnumerable<long> progressUsers)
    {
        var assigned = assignmentUsers.Distinct().ToArray();
        return cleaner > 0 && assigned.Length == 1 && assigned[0] == cleaner
            && progressUsers.All(user => user == cleaner);
    }

    public static bool Matches(CleanerAttribution source, long task, long room, long branch,
        long currentCycle, byte[] persistedCompletionRevision, bool sourceIntact, bool cleanerEligible)
        => task > 0 && room > 0 && branch > 0 && source.CleaningTaskId == task && task == currentCycle
        && source.RoomId == room && source.BranchId == branch && source.AssignmentId > 0
        && source.CleanerUserId > 0 && source.CompletionProgressId > 0 && source.WriterTransactionId > 0
        && source.CompletedAtUtc.Kind == DateTimeKind.Utc && source.CompletionRevision.Length == 8
        && persistedCompletionRevision.Length == 8 && source.CompletionRevision.SequenceEqual(persistedCompletionRevision)
        && sourceIntact && cleanerEligible;
}
