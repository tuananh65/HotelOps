using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// Extends the single existing foundation. No queue, receipt table, producer profile or worker.
public sealed class Fe05NotificationStaging(HotelOpsDbContext db, NotificationCommitGuard guard,
    ICurrentUserAccessor current, IFe05NotificationSource source, IFe05NotificationRecipientPolicy recipients)
{
    public async Task<Fe05NotificationCandidate> PrepareAsync(Fe05NotificationCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId <= 0 || actor.SessionId <= 0 || actor.IsRestricted)
            throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
        var snapshot = await source.LoadLockedAsync(command, actor.UserId, ct);
        var resolved = await recipients.ResolveAsync(snapshot, command.Kind, ct);
        return Fe05NotificationMapping.Map(command, snapshot, actor.UserId, resolved);
    }

    public async Task<NotificationStaged> StageRequiredAsync(DbConnection callerConnection,
        DbTransaction callerTransaction, Fe05NotificationCommand command, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var tx = db.Database.CurrentTransaction;
            if (tx is null || !ReferenceEquals(db.Database.GetDbConnection(), callerConnection) ||
                !ReferenceEquals(tx.GetDbTransaction(), callerTransaction) ||
                !ReferenceEquals(callerTransaction.Connection, callerConnection))
                throw new InvalidOperationException("The exact caller-owned EF connection and transaction are required.");
            guard.RequireAttached(db, tx.TransactionId);
            _ = await PrepareAsync(command, ct);
            // C04 cannot persist operation GUID, source revision and multi-recipient receipt.
            // FS013 has another publication graph. Never enqueue into a second graph or return
            // a fake pending/durable receipt. A config flag alone cannot remove this boundary.
            throw new Fe05NotificationUnavailableException("FE05_OPERATION_GRAPH_AND_DURABLE_RECEIPT_NOT_ACCEPTED");
        }
        catch
        {
            if (db.Database.CurrentTransaction is { } failed) guard.Fail(db, failed.TransactionId);
            throw;
        }
    }
}
