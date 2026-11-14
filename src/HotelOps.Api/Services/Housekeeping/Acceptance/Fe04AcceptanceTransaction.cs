using System.Data;
using System.Data.Common;

namespace HotelOps.Api.Services.Housekeeping.Acceptance;

public static class Fe04AcceptanceTransaction
{
    public static void Require(DbConnection expected, DbTransaction physical)
    {
        if (!ReferenceEquals(physical.Connection, expected) || physical.IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("ONE_CALLER_OWNED_SERIALIZABLE_TRANSACTION_REQUIRED");
    }

    public static async Task RollbackOnExitAsync(DbConnection expected, DbTransaction physical,
        Func<CancellationToken, Task> probe, CancellationToken ct)
    {
        Require(expected, physical);
        try { ct.ThrowIfCancellationRequested(); await probe(ct); }
        finally
        {
            // If the probe committed/disposed independently, refuse to label the round trip successful.
            if (!ReferenceEquals(physical.Connection, expected))
                throw new InvalidOperationException("PROBE_MUST_NOT_COMMIT_OR_REPLACE_CALLER_TRANSACTION");
            await physical.RollbackAsync(CancellationToken.None);
        }
    }
}
