using System.Data;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// FS015 extension of the existing candidate publisher and SAME publication/intent ledger.
// Explicit Development + exact fresh trial + marker + opt-in; never shared acceptance.
public sealed partial class ProvisionalCleaningNotification
{
    public async Task<bool> ClosureAvailableAsync(CancellationToken ct)
    {
        if (!env.IsDevelopment() || config["CleaningClosure:Candidate"] != "FS015_PROVISIONAL"
            || db.Database.GetDbConnection().Database != "HotelOps_Canonical_FS015_Cleaning_V1_Fresh") return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME()
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS015.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS015')
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS015.Closure' AND CONVERT(nvarchar(128),value)='PROVISIONAL_V1')
            AND COL_LENGTH('dbo.notification_publications','occurrence_key') IS NOT NULL
            AND OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions','U') IS NOT NULL
            AND OBJECT_ID('dbo.notification_delivery_intents','U') IS NOT NULL
            THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(ct) == 1;
    }

    public async Task<long[]> ClosureRecipientsAsync(long branch, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var ids = await db.UserRoles.AsNoTracking().Where(r => r.BranchId == branch && r.IsActive && r.Role.IsActive
            && r.Role.RoleCode == "HK_SUPERVISOR" && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now)
            && r.User.AccountStatus == "ACTIVE" && r.User.EmploymentStatus == "ACTIVE" && r.User.DeletedAt == null
            && db.UserBranchAccesses.Any(a => a.UserId == r.UserId && a.BranchId == branch && a.RevokedAt == null && a.GrantedAt <= now))
            .Select(r => r.UserId).Distinct().Order().ToArrayAsync(ct);
        var result = new List<long>();
        var data = new EfPermissionDataSource(db);
        foreach (var id in ids)
            if ((await data.ReadPathsAsync(id, "CLEANING_VIEW", now, ct)).Any(p => p.PermissionCode == "CLEANING_VIEW"
                && PermissionGrantRules.IsEligible(p, now) && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                    || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) == "BRANCH"))) result.Add(id);
        return result.ToArray();
    }

    public async Task StageClosureAsync(long actor, byte[] fingerprint, long room, long[] recipients,
        CleaningClosureResult result, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !await ClosureAvailableAsync(ct) || recipients.Length == 0)
            throw new InvalidOperationException("MANDATORY_CLOSURE_STAGING_UNAVAILABLE");
        var eventName = result.State == "ON_HOLD" ? "CLEANING_BLOCKER_REPORTED" : "CLEANING_TASK_COMPLETED";
        var occurrence = result.State == "ON_HOLD" ? $"CLEANING:{result.Id}:BLOCKER:{result.ProgressId}" : $"CLEANING:{result.Id}:COMPLETE";
        var payload = JsonSerializer.Serialize(new { schemaVersion = 1, eventName, occurrenceKey = occurrence,
            sourceType = "CLEANING_TASK", taskId = result.Id, taskCycleId = result.Id, roomId = room,
            actorId = actor, result.BranchId, result.AssignmentId, result.ProgressId, result.OperationId,
            result.PublicationId, result.OccurredAtUtc, result.SourceETag }, Json);
        var json = JsonSerializer.Serialize(result, Json);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
            VALUES({actor},{result.OperationId},1,{fingerprint},1,{json},{result.OccurredAtUtc});
            INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,assignment_id,source_revision,payload_json,created_at,occurrence_key)
            VALUES({result.PublicationId},{actor},{result.OperationId},{eventName},1,{result.AssignmentId},{Convert.FromHexString(result.SourceETag.Trim('"'))},{payload},{result.OccurredAtUtc},{occurrence});
            """, ct);
        foreach (var recipient in recipients)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.notification_delivery_intents(publication_id,user_id,delivery_channel,status,next_attempt_at)
                VALUES({result.PublicationId},{recipient},'IN_APP','PENDING',{result.OccurredAtUtc})
                """, ct);
        await VerifyClosureAsync(actor, result.OperationId, recipients.Length, ct);
    }

    public async Task VerifyClosureAsync(long actor, Guid operation, int recipients, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || recipients < 1) throw new InvalidOperationException("CALLER_TRANSACTION_REQUIRED");
        var proof = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.notification_operation_reservations o
            JOIN dbo.notification_publications p ON p.actor_user_id=o.actor_user_id AND p.operation_key=o.operation_key
            WHERE o.actor_user_id={actor} AND o.operation_key={operation} AND o.completed=1
            AND p.event_type IN('CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED') AND p.schema_version=1
            AND JSON_VALUE(o.result_json,'$.sourceETag')=CHAR(34)+CONVERT(varchar(16),p.source_revision,2)+CHAR(34)
            AND (SELECT COUNT(*) FROM dbo.notification_delivery_intents i WHERE i.publication_id=p.publication_id)={recipients}
            AND EXISTS(SELECT 1 FROM dbo.audit_logs a WHERE a.actor_user_id={actor} AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(a.new_values,'$.operationId'))={operation})
            """).SingleAsync(ct);
        if (proof != 1) throw new InvalidOperationException("MANDATORY_CLOSURE_PROOF_INCOMPLETE");
    }
}

public sealed partial class ProvisionalCleaningDelivery
{
    private sealed class ClosureIntent
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long BranchId { get; set; }
        public long TaskId { get; set; }
        public long ActorId { get; set; }
        public string EventName { get; set; } = "";
    }
    private async Task<string> DeliverClosureOneAsync(CancellationToken ct, Guid? publication)
    {
        var selected = await db.Database.SqlQuery<ClosureIntent>($"""
            SELECT TOP(1) i.intent_id AS Id,i.user_id AS UserId,p.actor_user_id AS ActorId,p.event_type AS EventName,
            TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.branchId')) AS BranchId,
            TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.taskId')) AS TaskId
            FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
            WHERE i.status='PENDING' AND i.next_attempt_at<=SYSUTCDATETIME()
            AND p.event_type IN('CLEANING_BLOCKER_REPORTED','CLEANING_TASK_COMPLETED')
            AND ({publication} IS NULL OR p.publication_id={publication}) ORDER BY i.intent_id
            """).SingleOrDefaultAsync(ct);
        if (selected == null) return "EMPTY";
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await new UserEligibilityLock(db).LockAsync([selected.UserId], ct);
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WITH(UPDLOCK,HOLDLOCK) WHERE intent_id={selected.Id} AND status='PENDING'").SingleAsync(ct) != 1) return "EMPTY";
            if (!(await candidate.ClosureRecipientsAsync(selected.BranchId, ct)).Contains(selected.UserId)
                || !await db.CleaningTasks.AsNoTracking().AnyAsync(t => t.CleaningTaskId == selected.TaskId && t.Room.BranchId == selected.BranchId, ct))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET status='TERMINAL',failure_code='RECIPIENT_OR_SOURCE_DENIED',attempt_count=attempt_count+1 WHERE intent_id={selected.Id}", ct);
                await tx.CommitAsync(ct); return "RECIPIENT_OR_SOURCE_DENIED";
            }
            var notice = new Notification { NotificationType = selected.EventName, Title = "Cleaning task update",
                Message = selected.EventName == "CLEANING_BLOCKER_REPORTED" ? "A cleaning task is on hold." : "A cleaning task is pending inspection.",
                Priority = "NORMAL", SourceType = "CLEANING_TASK", SourceId = selected.TaskId,
                ActionUrl = $"/cleaning/tasks/{selected.TaskId}?branchId={selected.BranchId}", CreatedBy = selected.ActorId, CreatedAt = clock.GetUtcNow().UtcDateTime };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
            db.NotificationRecipients.Add(new() { NotificationId = notice.NotificationId, UserId = selected.UserId,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = notice.CreatedAt });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET status='DELIVERED',inbox_notification_id={notice.NotificationId},attempt_count=attempt_count+1,failure_code=NULL WHERE intent_id={selected.Id}", ct);
            await tx.CommitAsync(ct); return "DELIVERED";
        }
        catch (Exception e) when (!ct.IsCancellationRequested && e is System.Data.Common.DbException or DbUpdateException)
        {
            db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET attempt_count=attempt_count+1,next_attempt_at=DATEADD(second,5,SYSUTCDATETIME()),failure_code='TRANSIENT_DELIVERY_ERROR' WHERE intent_id={selected.Id} AND status='PENDING'", ct);
            return "RETRY_SCHEDULED";
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
