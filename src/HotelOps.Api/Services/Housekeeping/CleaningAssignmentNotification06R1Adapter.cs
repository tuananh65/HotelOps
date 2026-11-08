#if HOTELOPS_NOTIFICATION_06R1
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Housekeeping;

// Compile only in the disposable 06R1 composition until the external blockers are resolved.
// Not registered in Program; never replaces the FS013 producer/graph or runs a second publisher.
public sealed class CleaningAssignmentNotification06R1Profile(CleaningAssignmentNotificationSource source) : INotificationEventProfile
{
    public string EventType => CleaningAssignmentNotificationRules.EventType;
    public short Version => CleaningAssignmentNotificationRules.Version;

    public async Task<ResolvedNotification> ResolveAsync(long occurrenceId, CancellationToken ct)
    {
        var content = await source.ResolveAsync(occurrenceId, staging: true, ct);
        return new(content.Facts.TaskId, content.Facts.BranchId, content.Facts.AssigneeUserId,
            content.ReadPolicy, content.Title, content.Message, content.ActionPath);
    }
}

// Keep the profile independent of the participant to avoid a publisher/profile DI cycle.
public sealed class CleaningAssignmentNotification06R1Adapter(HotelOpsDbContext db,
    CleaningAssignmentNotificationSource source, NotificationTransactionParticipant participant,
    NotificationCommitGuard guard)
{
    public async Task<NotificationStaged> StageAsync(DbConnection connection, DbTransaction transaction,
        long assignmentId, long actor, Guid operation, byte[] fingerprint, CancellationToken ct)
    {
        // No context/transaction creation, SaveChanges, Commit, recovery or swallowed failures here.
        try
        {
            RequireCaller(connection, transaction);
            var persisted = await source.LoadAsync(assignmentId, ct);
            CleaningAssignmentNotificationRules.RequireCommand(persisted, actor, operation, fingerprint);
            return await participant.PublishRequiredIntentsAsync(connection, transaction,
                new(CleaningAssignmentNotificationRules.EventType, CleaningAssignmentNotificationRules.Version, assignmentId), ct);
        }
        catch
        {
            if (db.Database.CurrentTransaction is { } current) guard.Fail(db, current.TransactionId);
            throw;
        }
    }

    public Task VerifyBeforeCommitAsync(DbConnection connection, DbTransaction transaction, CancellationToken ct)
        => participant.VerifyBeforeCommitAsync(connection, transaction, ct);

    private void RequireCaller(DbConnection connection, DbTransaction transaction)
    {
        if (db.Database.CurrentTransaction is not { } current ||
            !ReferenceEquals(connection, db.Database.GetDbConnection()) ||
            !ReferenceEquals(transaction, current.GetDbTransaction()) || !ReferenceEquals(transaction.Connection, connection))
            throw new InvalidOperationException("UC029_CALLER_TRANSACTION_REQUIRED");
    }
}
#endif
