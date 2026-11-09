using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// Transaction-owned application lock is the claim. A crash releases it and rolls back the inbox graph.
public sealed class NotificationDelivery(HotelOpsDbContext db, NotificationSourcePolicy policy, TimeProvider clock) : INotificationDeliveryBatch
{
    public async Task<int> RunBatchAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Delivery owns a clean, independent transaction.");
        var now = clock.GetUtcNow().UtcDateTime;
        var ids = await db.NotificationIntents.AsNoTracking().Where(x =>
            (x.State == "PENDING" || x.State == "FAILED") && x.NextAttemptUtc <= now)
            .OrderBy(x => x.IntentId).Select(x => x.IntentId).Take(50).ToListAsync(ct);
        var delivered = 0;
        foreach (var id in ids)
        {
            try
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.C04.NotificationDelivery',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=10000; IF @r<0 THROW 51004,'Notification delivery serialization unavailable',1;", ct);
                var intent = await db.NotificationIntents.SingleAsync(x => x.IntentId == id, ct);
                await db.Entry(intent).ReloadAsync(ct);
                if (intent.State is not ("PENDING" or "FAILED") || intent.NextAttemptUtc > now) continue;
                intent.Attempts = checked(intent.Attempts + 1);
                if (!NotificationPublisher.Digest(intent).SequenceEqual(intent.PayloadHash))
                    throw new InvalidOperationException("Notification payload integrity failure.");
                var source = NotificationSourceReference.Resolve(intent) ??
                    throw new NotificationContractUnavailableException();
                if (!await policy.AllowsAsync(intent, ct))
                {
                    intent.State = "DENIED"; intent.FailureCode = "SOURCE_ACCESS_DENIED";
                }
                else
                {
                    var notification = new Notification { NotificationType = intent.EventType,
                        Title = intent.Title, Message = intent.Message, Priority = "NORMAL", SourceType = source.SourceType,
                        SourceId = source.SourceId, ActionUrl = intent.ActionPath, CreatedAt = now };
                    db.Notifications.Add(notification);
                    await db.SaveChangesAsync(ct);
                    db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notification.NotificationId,
                        UserId = intent.RecipientUserId, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
                    intent.NotificationId = notification.NotificationId; intent.State = "DELIVERED"; intent.FailureCode = null;
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                if (intent.State == "DELIVERED") delivered++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (!NotificationDeliveryFailures.IsDeadlock(ex))
            {
                db.ChangeTracker.Clear();
                var retryable = NotificationDeliveryFailures.IsRetryable(ex);
                // Preserve permanent failures for operator review, without a hot retry loop.
                // No provider message or payload is persisted in diagnostics.
                await db.NotificationIntents.Where(x => x.IntentId == id && (x.State == "PENDING" || x.State == "FAILED"))
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.State, "FAILED")
                        .SetProperty(x => x.FailureCode, retryable ? "DELIVERY_UNAVAILABLE" : "DELIVERY_REQUIRES_REVIEW")
                        .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                        .SetProperty(x => x.NextAttemptUtc, retryable ? now.AddMinutes(5) : DateTime.MaxValue), ct);
                if (!retryable) throw;
            }
            finally { db.ChangeTracker.Clear(); }
        }
        return delivered;
    }
}
