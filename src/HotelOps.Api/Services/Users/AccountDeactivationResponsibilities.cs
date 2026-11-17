using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Users;

/// <summary>
/// Account deactivation uses current, global target responsibilities. The historical
/// DEV2 handoff diagnostic is not a runtime eligibility contract; role revocation keeps
/// its separate before/after capability assessment.
/// </summary>
internal sealed class AccountDeactivationResponsibilities(HotelOpsDbContext db)
{
    private Guid? transactionId;
    private long? targetId;

    internal async Task<Dev1ResponsibilityReview> ReadAsync(long target, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction;
        if (!db.Database.IsSqlServer() || tx is null
            || tx.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable
            || transactionId is { } previous && previous != tx.TransactionId
            || targetId is { } previousTarget && previousTarget != target)
            throw new InvalidOperationException("Matching Serializable account lifecycle transaction required.");
        transactionId = tx.TransactionId;
        targetId = target;
        var review = await new IntegratedDeactivationResponsibilityQueries(db).ReadAsync(target, ct);
        var unresolved = new SortedSet<string>(review.Unresolved, StringComparer.Ordinal);
        // ReceivedBy is historical only with an actual receipt time. Do not assume a
        // legacy named receiver with no receipt has no outstanding next action.
        if (await db.MaterialFulfillments.AsNoTracking().AnyAsync(f => f.ReceivedBy == target
            && (f.ReceivedAt == null || f.ReceivedAt < f.FulfilledAt), ct))
            unresolved.Add("MATERIAL_RECEIVER_RECEIPT_UNRESOLVED");
        return new(review.Blockers, unresolved.ToArray()) { Unsupported = review.Unsupported };
    }
}
