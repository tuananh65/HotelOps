using System.Data;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.WorkTime;

internal static class ReworkWorkdayBoundary
{
    // Physical IN_PROGRESS intentionally survives Hold and submission. It cannot by itself
    // establish that the executor has stopped. Caller retains user/time locks until commit.
    internal static async Task<bool> IsStoppedAsync(HotelOpsDbContext db, long actor, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new WorkTimeContractException("TIME_TRANSACTION_REQUIRED");
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN COL_LENGTH('dbo.cleaning_reworks','workflow_status') IS NOT NULL
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) != 1) return false;
        // Across branches, just like the single Working workday/open interval invariant.
        // Missing or unknown workflow states require reconciliation, never a guessed stop.
        return !await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.cleaning_reworks WITH(HOLDLOCK)
            WHERE assignee_user_id={actor} AND status='IN_PROGRESS'
              AND (workflow_status IS NULL OR workflow_status NOT IN ('ON_HOLD','PENDING_REINSPECTION'))
            """).AnyAsync(ct);
    }
}
