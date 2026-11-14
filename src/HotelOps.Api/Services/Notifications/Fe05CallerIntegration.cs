using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// Compilable call-site seam. DEV1 retains authorization, source writes, SaveChanges,
// audit, commit and rollback. This currently throws closed; it cannot unblock UC045/046.
public static class Fe05CallerIntegration
{
    // Reserve BEFORE authoritative mutation; if Replay is non-null, skip the mutation entirely.
    // This explicit candidate participant must be composed by DEV1/schema/runtime owners.
    public static Task<Fe05OperationLease> ReserveOperationAsync(HotelOpsDbContext db,
        Fe05OperationStaging staging, Fe05OperationRequest request, CancellationToken ct)
    {
        var tx=db.Database.CurrentTransaction ?? throw new InvalidOperationException("Caller transaction required.");
        return staging.ReserveAsync(db.Database.GetDbConnection(),tx.GetDbTransaction(),request,ct);
    }
    public static Task<Fe05OperationReceipt> StageOperationAsync(HotelOpsDbContext db,
        Fe05OperationStaging staging, Fe05OperationLease lease,
        IReadOnlyList<Fe05NotificationCommand> commands, CancellationToken ct)
    {
        var tx=db.Database.CurrentTransaction ?? throw new InvalidOperationException("Caller transaction required.");
        return staging.StageRequiredAsync(db.Database.GetDbConnection(),tx.GetDbTransaction(),lease,commands,ct);
    }

    public static Task<NotificationStaged> StageInCallerTransactionAsync(HotelOpsDbContext db,
        Fe05NotificationStaging staging, Fe05NotificationCommand command, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Caller transaction required.");
        return staging.StageRequiredAsync(db.Database.GetDbConnection(), tx.GetDbTransaction(), command, ct);
    }
}
