using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Notifications;

// Durable reassignment outbox; recipients are checked again at delivery and source opening.
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
    public sealed class PendingIntent
    {
        public long Id { get; set; }
        public long Actor { get; set; }
        public Guid Operation { get; set; }
    }
    public async Task<PendingIntent[]> PendingAsync(CancellationToken ct,long after=0)
    {
        if (!await CleaningReassignmentSchema.InstalledAsync(db, ct) || !await candidate.AvailableAsync(ct)) return [];
        return await db.Database.SqlQuery<PendingIntent>($"""
            SELECT TOP(100) intent_id AS Id,actor_user_id AS Actor,operation_key AS Operation
            FROM dbo.fs013_reassignment_intents WHERE status='PENDING' AND next_attempt_at<=SYSUTCDATETIME() AND intent_id>{after}
            ORDER BY intent_id
            """).ToArrayAsync(ct);
    }

    internal static async Task<string?> TargetAsync(HotelOpsDbContext db, RoomTypeCreationService source,
        long notification, long user, CancellationToken ct)
    {
        if (!await CleaningReassignmentSchema.InstalledAsync(db, ct)) return null;
        var rows = await db.Database.SqlQuery<Intent>($"""
            SELECT i.intent_id AS Id,i.user_id AS UserId,o.task_id AS TaskId,o.branch_id AS BranchId,
              o.assignment_id AS AssignmentId,o.previous_assignment_id AS PreviousAssignmentId,i.recipient_kind AS RecipientKind
            FROM dbo.fs013_reassignment_intents i JOIN dbo.fs013_reassignment_operations o
              ON o.actor_user_id=i.actor_user_id AND o.operation_key=i.operation_key
            JOIN dbo.notifications n ON n.notification_id=i.inbox_notification_id
            JOIN dbo.cleaning_assignments a ON a.assignment_id=o.assignment_id
            JOIN dbo.cleaning_assignments p ON p.assignment_id=o.previous_assignment_id
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=o.task_id JOIN dbo.rooms r ON r.room_id=t.room_id
            WHERE i.inbox_notification_id={notification} AND i.user_id={user} AND i.status='DELIVERED' AND i.recipient_kind='NEW'
              AND n.created_by=o.actor_user_id AND n.created_at>=o.occurred_at
              AND n.notification_type='CLEANING_TASK_ASSIGNED' AND n.source_type='CLEANING_TASK' AND n.source_id=o.task_id
              AND a.assignee_user_id=i.user_id AND a.cleaning_task_id=o.task_id AND a.assigned_by=o.actor_user_id
              AND a.assigned_at=o.occurred_at AND p.cleaning_task_id=o.task_id AND p.ended_at=o.occurred_at
              AND p.assignment_status='REASSIGNED' AND r.branch_id=o.branch_id
              AND EXISTS(SELECT 1 FROM dbo.user_branch_access uba WHERE uba.user_id=i.user_id AND uba.branch_id=o.branch_id
                AND uba.revoked_at IS NULL AND uba.granted_at<=SYSUTCDATETIME())
              AND EXISTS(SELECT 1 FROM dbo.audit_logs l WHERE l.entity_type='cleaning_tasks'
                AND l.entity_id=CONVERT(nvarchar(30),o.task_id) AND l.actor_user_id=o.actor_user_id
                AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(l.new_values,'$.operationId'))=o.operation_key
                AND TRY_CONVERT(bigint,JSON_VALUE(l.new_values,'$.assignmentId'))=o.assignment_id)
            """).ToArrayAsync(ct);
        if (rows.Length != 1) return null;
        var row = rows[0];
        if (!await source.CanReadAssignedCleaningAsync(user, row.BranchId, row.TaskId, row.AssignmentId, ct)) return null;
        return $"/cleaning/tasks/{row.TaskId}?branchId={row.BranchId}&assignmentId={row.AssignmentId}";
    }
    public async Task<string> DeliverOneAsync(long actor, Guid operation, CancellationToken ct,long? intentId=null)
    {
        if ((!await CleaningReassignmentSchema.InstalledAsync(db, ct)
            && db.Database.GetDbConnection().Database != "HotelOps_Canonical_20261106_f130001_Fresh")
            || !await candidate.AvailableAsync(ct)) throw new InvalidOperationException("REASSIGNMENT_CAPABILITY_REQUIRED");
        var selected = await db.Database.SqlQuery<Intent>($"SELECT TOP(1) i.intent_id AS Id,i.user_id AS UserId,o.task_id AS TaskId,o.branch_id AS BranchId,o.assignment_id AS AssignmentId,o.previous_assignment_id AS PreviousAssignmentId,i.recipient_kind AS RecipientKind FROM dbo.fs013_reassignment_intents i JOIN dbo.fs013_reassignment_operations o ON o.actor_user_id=i.actor_user_id AND o.operation_key=i.operation_key WHERE i.actor_user_id={actor} AND i.operation_key={operation} AND ({intentId} IS NULL OR i.intent_id={intentId}) AND i.status='PENDING' AND i.next_attempt_at<=SYSUTCDATETIME() ORDER BY i.intent_id").SingleOrDefaultAsync(ct);
        if (selected == null) return "EMPTY";
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await new UserEligibilityLock(db).LockAsync([selected.UserId], ct);
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.fs013_reassignment_intents WITH(UPDLOCK,HOLDLOCK) WHERE intent_id={selected.Id} AND status='PENDING' AND next_attempt_at<={now}").SingleAsync(ct) != 1) return "EMPTY";
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fs013_verify_reassignment @actor={actor},@operation={operation},@mutation={false}",ct);
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
            if (!previous && await CleaningReassignmentSchema.InstalledAsync(db, ct)
                && await TargetAsync(db, source, notice.NotificationId, selected.UserId, ct) == null)
                throw new InvalidOperationException("REASSIGNMENT_SOURCE_CHANGED");
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
