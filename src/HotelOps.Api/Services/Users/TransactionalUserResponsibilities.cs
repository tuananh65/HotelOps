using HotelOps.Api.Data;
using HotelOps.Api.Services.Dependencies;

namespace HotelOps.Api.Services.Users;

/// <summary>One owner transaction, one target, retained User locks and a concrete DEV2 reader lease.</summary>
internal sealed class TransactionalUserResponsibilities(HotelOpsDbContext db, Dev2DependencyReader? reader)
{
    private GlobalUserDependencySnapshot? initial;
    private Guid? transaction;
    private long target;

    internal async Task<Dev1ResponsibilityReview> ReadAsync(long targetId, bool afterMutation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (db.Database.CurrentTransaction is not { } tx || transaction is { } existing && (existing != tx.TransactionId || target != targetId))
            throw new InvalidOperationException("Matching owner transaction and target required.");
        if (afterMutation && initial is null)
            throw new InvalidOperationException("A pre-write responsibility snapshot is required.");
        transaction = tx.TransactionId;
        target = targetId;
        var legacy = await new IntegratedDeactivationResponsibilityQueries(db).ReadAsync(targetId, ct);
        var blockers = new SortedSet<string>(legacy.Blockers, StringComparer.Ordinal);
        var gaps = new SortedSet<string>(legacy.Unresolved, StringComparer.Ordinal);
        if (reader is null) gaps.Add("DEV2_TRANSACTIONAL_READER_REQUIRED");
        else
        {
            try
            {
                var fresh = initial is null || afterMutation
                    ? await reader.ReadUserGlobalInCallerTransactionAsync(targetId, ct)
                    : await reader.RevalidateUserGlobalInCallerTransactionAsync(targetId, initial, ct);
                var assessment = UserResponsibilitySafety.Assess(targetId, fresh);
                blockers.UnionWith(assessment.Blockers);
                gaps.UnionWith(assessment.Unresolved);
                if (initial is not null && UserResponsibilitySafety.ResponsibilityFingerprint(initial) != UserResponsibilitySafety.ResponsibilityFingerprint(fresh))
                    gaps.Add("DEV2_RESPONSIBILITIES_CHANGED");
                initial ??= fresh;
            }
            catch (DependencyScopeException) { gaps.Add("DEV2_RESPONSIBILITY_AUTHORITY_UNAVAILABLE"); }
            catch (DependencySnapshotConflictException) { gaps.Add("DEV2_RESPONSIBILITIES_CHANGED"); }
            // SQL failures/cancellation propagate and roll back the owner transaction.
        }
        return new(blockers.ToArray(), gaps.ToArray()) { Unsupported = legacy.Unsupported };
    }
}
