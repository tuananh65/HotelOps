using System.Data.Common;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// Candidate integration seam for callers that retain the physical transaction.
// This does not translate the unapproved DEV1/V12 operation graph or cleaning event.
public sealed class NotificationTransactionParticipant(HotelOpsDbContext db, INotificationPublisher publisher,
    NotificationCommitGuard guard)
{
    public async Task<NotificationStaged> PublishRequiredIntentsAsync(DbConnection callerConnection,
        DbTransaction callerTransaction, NotificationEvent sourceEvent, CancellationToken ct)
    {
        try
        {
            RequireSameTransaction(callerConnection, callerTransaction);
            return await publisher.PublishRequiredIntentsAsync(sourceEvent, ct);
        }
        catch { PoisonCurrentTransaction(); throw; }
    }

    public async Task VerifyBeforeCommitAsync(DbConnection callerConnection, DbTransaction callerTransaction, CancellationToken ct)
    {
        try
        {
            RequireSameTransaction(callerConnection, callerTransaction);
            await guard.VerifyBeforeCommitAsync(db, ct);
        }
        catch { PoisonCurrentTransaction(); throw; }
    }

    private void PoisonCurrentTransaction()
    {
        if (db.Database.CurrentTransaction is { } current) guard.Fail(db, current.TransactionId);
    }

    private void RequireSameTransaction(DbConnection connection, DbTransaction transaction)
    {
        if (db.Database.CurrentTransaction is not { } current ||
            !ReferenceEquals(connection, db.Database.GetDbConnection()) ||
            !ReferenceEquals(transaction, current.GetDbTransaction()) || !ReferenceEquals(transaction.Connection, connection))
            throw new InvalidOperationException("The exact scoped caller DbContext, DbConnection and DbTransaction are required.");
        guard.RequireAttached(db, current.TransactionId);
    }
}
