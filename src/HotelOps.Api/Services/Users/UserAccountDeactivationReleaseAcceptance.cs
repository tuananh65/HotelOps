using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Users;

/// <summary>
/// Runtime boundary for the integrated account workflow. Current responsibility readers
/// replace historical DEV1/DEV2 handoff flags. Assignment writers retain ordered User
/// locks (RoomTypeCreationService.BatchAsync, IR assignment/handover, maintenance
/// assignment, shift planning and task series generation); the owner retains the global
/// Users/authority snapshot and rechecks responsibilities before and after mutation.
/// </summary>
internal static class UserAccountDeactivationReleaseAcceptance
{
    internal static void EnsureAccepted(HotelOpsDbContext db)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not { } transaction
            || transaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable
            || System.Transactions.Transaction.Current is not null)
            throw new DeactivationReleaseNotAcceptedException();
    }
}

internal sealed class DeactivationReleaseNotAcceptedException()
    : InvalidOperationException("Deactivation requires its owned Serializable SQL transaction.");
