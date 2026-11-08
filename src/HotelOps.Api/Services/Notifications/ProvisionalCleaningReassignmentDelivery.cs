using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Notifications;

// Explicit developer invocation only. No hosted service, accepted publisher claim or global queue drain.
public sealed class ProvisionalCleaningReassignmentDelivery(HotelOpsDbContext db, ProvisionalCleaningNotification candidate,
    RoomTypeCreationService source, TimeProvider clock)
{
    public sealed class Intent
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long TaskId { get; set; }
        public long BranchId { get; set; }
        public long AssignmentId { get; set; }
        public long PreviousAssignmentId { get; set; }
        public string RecipientKind { get; set; } = "";
    }
    public async Task<string> DeliverOneAsync(long actor, Guid operation, CancellationToken ct)
    {
        if (db.Database.GetDbConnection().Database != "HotelOps_Canonical_20261106_f130001_Fresh" || !await candidate.AvailableAsync(ct)) throw new InvalidOperationException("FS013_CANDIDATE_REQUIRED");
        var selected = await db.Database.SqlQuery<Intent>($"SELECT TOP(1) i.intent_id AS Id,i.user_id AS UserId,o.task_id AS TaskId,o.branch_id AS BranchId,o.assignment_id AS AssignmentId,o.previous_assignment_id AS PreviousAssignmentId,i.recipient_kind AS RecipientKind FROM dbo.fs013_reassignment_intents i JOIN dbo.fs013_reassignment_operations o ON o.actor_user_id=i.actor_user_id AND o.operation_key=i.operation_key WHERE i.actor_user_id={actor} AND i.operation_key={operation} AND i.status='PENDING' AND i.next_attempt_at<=SYSUTCDATETIME() ORDER BY i.intent_id").SingleOrDefaultAsync(ct);
        if (selected == null) return "EMPTY";
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await new UserEligibilityLock(db).LockAsync([selected.UserId], ct);
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.fs013_reassignment_intents WITH(UPDLOCK,HOLDLOCK) WHERE intent_id={selected.Id} AND status='PENDING' AND next_attempt_at<={now}").SingleAsync(ct) != 1) return "EMPTY";
            var data = new EfPermissionDataSource(db);
            var active = await data.IsActiveUserAsync(selected.UserId, ct) && await data.IsActiveBranchAsync(selected.BranchId, ct)
                && await db.UserBranchAccesses.AsNoTracking().AnyAsync(x => x.UserId == selected.UserId && x.BranchId == selected.BranchId && x.RevokedAt == null && x.GrantedAt <= now, ct);
            var allowed = false;
            if (active && selected.RecipientKind == "NEW") allowed = await source.CanReadAssignedCleaningAsync(selected.UserId, selected.BranchId, selected.TaskId, selected.AssignmentId, ct);
            if (active && selected.RecipientKind == "PREVIOUS")
            {
                // Historical membership permits ONLY a content-minimal ended-assignment receipt,
                // never continuing source-read access to another housekeeper's current task.
                var member = await db.CleaningAssignments.AsNoTracking().AnyAsync(x => x.AssignmentId == selected.PreviousAssignmentId && x.AssigneeUserId == selected.UserId
                    && x.CleaningTaskId == selected.TaskId && x.EndedAt != null && x.CleaningTask.Room.BranchId == selected.BranchId, ct);
                var paths = await data.ReadPathsAsync(selected.UserId, "CLEANING_VIEW", now, ct);
                allowed = member && paths.Any(p => p.PermissionCode == "CLEANING_VIEW" && PermissionGrantRules.IsEligible(p, now)
                    && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL" || p.BranchId == selected.BranchId && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN"));
            }
            if (!allowed)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs013_reassignment_intents SET status='TERMINAL',failure_code='RECIPIENT_OR_SOURCE_DENIED',attempt_count=attempt_count+1 WHERE intent_id={selected.Id}", ct);
                await tx.CommitAsync(ct); return "RECIPIENT_OR_SOURCE_DENIED";
            }
            var previous = selected.RecipientKind == "PREVIOUS";
            var notice = new Notification { NotificationType = "CLEANING_TASK_ASSIGNED", Title = previous ? "Cleaning assignment ended" : "Cleaning task assigned",
                Message = previous ? "Your cleaning assignment has ended." : "A cleaning task has been assigned to you.", Priority = "NORMAL",
                SourceType = previous ? null : "CLEANING_TASK", SourceId = previous ? null : selected.TaskId,
                ActionUrl = previous ? null : $"/cleaning/tasks/{selected.TaskId}?branchId={selected.BranchId}&assignmentId={selected.AssignmentId}", CreatedBy = actor, CreatedAt = now };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
            db.NotificationRecipients.Add(new() { NotificationId = notice.NotificationId, UserId = selected.UserId, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs013_reassignment_intents SET status='DELIVERED',inbox_notification_id={notice.NotificationId},attempt_count=attempt_count+1,failure_code=NULL WHERE intent_id={selected.Id}", ct);
            await tx.CommitAsync(ct); return "DELIVERED";
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is System.Data.Common.DbException or DbUpdateException)
        {
            db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.fs013_reassignment_intents SET attempt_count=attempt_count+1,next_attempt_at={now.AddSeconds(5)},failure_code='TRANSIENT_DELIVERY_ERROR' WHERE intent_id={selected.Id} AND status='PENDING'", ct);
            return "RETRY_SCHEDULED";
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
