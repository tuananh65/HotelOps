namespace HotelOps.Api.Services.WorkTime;

// REVIEW PROPOSAL. Trusted backend calls only; never model-bind these records from HTTP.
public sealed record WorkTimeOperation(long ActorUserId, long OwnerUserId, long BranchId,
    string WorkItemKind, long WorkItemId);
public sealed record WorkTimeIntervalResult(long IntervalId, long WorkdayId, DateTime OpenedAtUtc,
    DateTime? ClosedAtUtc, byte[] RowVersion);

public interface IWorkTimeTransactionParticipant
{
    // Caller begins SAME scoped HotelOpsDbContext transaction before this call.
    // Must precede all location/feature locks. Locks actor/owners in ascending ID order;
    // reads fresh eligibility; action-specific eligibility/time checks follow under those locks.
    // Result/error codes and authorization responsibilities are specified in DEV2 contract.
    Task LockUsersAsync(IReadOnlyCollection<long> actorAndOwnerIds, CancellationToken cancellationToken);

    // Caller must hold time/user locks, then lock and re-read authoritative source/owner/branch,
    // authorize source action and validate transition BEFORE invoking these methods.
    // Methods recheck time invariants and require the user set locked by this participant.
    // No caller timestamps, implicit commit, independent connection, queue, or HTTP callback.
    Task<WorkTimeIntervalResult> OpenIntervalAsync(WorkTimeOperation operation, CancellationToken cancellationToken);
    Task<WorkTimeIntervalResult> CloseIntervalAsync(WorkTimeOperation operation, long intervalId,
        byte[] expectedRowVersion, CancellationToken cancellationToken);
}
