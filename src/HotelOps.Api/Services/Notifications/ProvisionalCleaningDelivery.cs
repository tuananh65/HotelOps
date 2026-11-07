using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Models;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Notifications;

// Explicitly invoked isolated candidate only. No background host or competing shared dispatcher registered.
public sealed partial class ProvisionalCleaningDelivery(HotelOpsDbContext db, ProvisionalCleaningNotification candidate, RoomTypeCreationService source, TimeProvider clock)
{
    public async Task<string> DeliverOneAsync(CancellationToken ct, bool stopAfterDurableClaim = false, Guid? selectedPublication = null)
    {
        if (await candidate.ClosureAvailableAsync(ct)) return await DeliverClosureOneAsync(ct, selectedPublication);
        if (!await candidate.AvailableAsync(ct)) throw new InvalidOperationException("CANDIDATE_DISABLED");
        var now = clock.GetUtcNow().UtcDateTime; long id; long recipient; Guid claim = Guid.NewGuid();
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            var query = selectedPublication is { } selected
                ? db.CandidateIntents.FromSqlInterpolated($"SELECT TOP(1) * FROM dbo.notification_delivery_intents WITH(UPDLOCK,READPAST,ROWLOCK) WHERE publication_id={selected} AND status='PENDING' AND next_attempt_at<={now} AND (claimed_until IS NULL OR claimed_until<{now}) ORDER BY intent_id")
                : db.CandidateIntents.FromSqlInterpolated($"SELECT TOP(1) * FROM dbo.notification_delivery_intents WITH(UPDLOCK,READPAST,ROWLOCK) WHERE status='PENDING' AND next_attempt_at<={now} AND (claimed_until IS NULL OR claimed_until<{now}) ORDER BY intent_id");
            var intent = await query.SingleOrDefaultAsync(ct);
            if (intent == null) return "EMPTY";
            id = intent.IntentId; recipient = intent.UserId; intent.ClaimId = claim; intent.ClaimedUntil = now.AddSeconds(30); intent.AttemptCount++;
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); await tx.CommitAsync(ct);
        }
        if (stopAfterDurableClaim) return "CLAIM_PERSISTED";
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await new UserEligibilityLock(db).LockAsync([recipient], ct);
            var intent = await db.CandidateIntents.FromSqlInterpolated($"SELECT * FROM dbo.notification_delivery_intents WITH(UPDLOCK,HOLDLOCK) WHERE intent_id={id} AND claim_id={claim} AND status='PENDING'").SingleOrDefaultAsync(ct);
            if (intent == null) return "CLAIM_LOST";
            var pub = await db.CandidatePublications.AsNoTracking().SingleAsync(x => x.PublicationId == intent.PublicationId, ct);
            var facts = JsonSerializer.Deserialize<CandidateAssignmentEvent>(pub.PayloadJson, ProvisionalCleaningNotification.Json) ?? throw new InvalidOperationException("INVALID_PAYLOAD");
            string? terminal = null;
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == recipient, ct);
            if (user == null) terminal = "RECIPIENT_NOT_FOUND";
            else if (user.AccountStatus != "ACTIVE" || user.EmploymentStatus != "ACTIVE" || user.DeletedAt != null) terminal = "RECIPIENT_INACTIVE";
            else if (!await db.UserBranchAccesses.AsNoTracking().AnyAsync(x => x.UserId == recipient && x.BranchId == facts.BranchId && x.RevokedAt == null && x.GrantedAt <= now, ct)) terminal = "RECIPIENT_OUT_OF_SCOPE";
            var assignment = await db.CleaningAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.AssignmentId == facts.AssignmentId && x.CleaningTaskId == facts.TaskId && x.AssigneeUserId == recipient && x.CleaningTask.Room.BranchId == facts.BranchId, ct);
            if (terminal == null && assignment == null) terminal = "SOURCE_NOT_FOUND";
            if (terminal == null && (assignment!.EndedAt != null || assignment.AssignmentStatus != "ACTIVE")) terminal = "SUPERSEDED";
            if (terminal == null && !await source.CanReadAssignedCleaningAsync(recipient, facts.BranchId, facts.TaskId, facts.AssignmentId, ct)) terminal = "SOURCE_READ_DENIED";
            if (terminal != null) { intent.Status = "TERMINAL"; intent.FailureCode = terminal; }
            else
            {
                // Entire inbox+recipient+intent linkage is one transaction, no external network delivery.
                var notification = new Notification { NotificationType = "CLEANING_TASK_ASSIGNED", Title = "Cleaning task assigned", Message = "A cleaning task has been assigned to you.", Priority = "NORMAL", SourceType = "CLEANING_TASK", SourceId = facts.TaskId, ActionUrl = $"/cleaning/tasks/{facts.TaskId}?branchId={facts.BranchId}&assignmentId={facts.AssignmentId}", CreatedBy = facts.ActorId, CreatedAt = now };
                db.Notifications.Add(notification); await db.SaveChangesAsync(ct);
                db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notification.NotificationId, UserId = recipient, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
                await db.SaveChangesAsync(ct);
                intent.Status = "DELIVERED"; intent.InboxNotificationId = notification.NotificationId; intent.FailureCode = null;
            }
            intent.ClaimId = null; intent.ClaimedUntil = null; await db.SaveChangesAsync(ct); db.ChangeTracker.Clear(); await tx.CommitAsync(ct);
            return terminal ?? "DELIVERED";
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            db.ChangeTracker.Clear();
            // Retryable error, never convert a database/source-service outage into permanent denied.
            await db.CandidateIntents.Where(x => x.IntentId == id && x.ClaimId == claim && x.Status == "PENDING")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ClaimId, (Guid?)null).SetProperty(x => x.ClaimedUntil, (DateTime?)null).SetProperty(x => x.NextAttemptAt, now.AddSeconds(5)).SetProperty(x => x.FailureCode, "TRANSIENT_DELIVERY_ERROR"), ct);
            for (Exception? cause = error; cause != null; cause = cause.InnerException)
                if (cause is Microsoft.Data.SqlClient.SqlException { Number: 1205 }) throw new InvalidOperationException("SQL_DEADLOCK_1205", error);
            return "RETRY_SCHEDULED";
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
