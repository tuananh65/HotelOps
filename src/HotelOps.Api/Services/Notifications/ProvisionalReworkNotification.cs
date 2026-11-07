using System.Data;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// FS016 extends the same candidate object/operation ledger. No additional publisher/dispatcher.
public sealed partial class ProvisionalCleaningNotification
{
    public async Task<bool> ReworkAvailableAsync(CancellationToken ct)
    {
        if (!env.IsDevelopment() || config["CleaningRework:Candidate"] != "FS016_PROVISIONAL"
            || db.Database.GetDbConnection().Database != "HotelOps_Canonical_FS016_Rework_V1_Fresh") return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME()
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS016.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS016')
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS016.Rework' AND CONVERT(nvarchar(128),value)='PROVISIONAL_NOT_ACCEPTED')
            AND OBJECT_ID('dbo.fs016_rework_source_proofs','U') IS NOT NULL
            AND OBJECT_ID('dbo.fs016_rework_evidence','U') IS NOT NULL
            AND COL_LENGTH('dbo.cleaning_rework_defects','remediation_note')=1000
            AND COL_LENGTH('dbo.notification_publications','rework_id')=8
            AND (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_rework_workflow_v1','CK_fs016_remediation','CK_fs016_publication_event','CK_fs016_publication_source') AND is_disabled=0 AND is_not_trusted=0)=4
            AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_fs016_remediation_revision','TR_fs016_evidence_revision') AND is_disabled=0)=2
            THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(ct) == 1;
    }

    public async Task RememberReworkAsync(long actor, byte[] fingerprint, long room, long[] recipients, ReworkCommandResult result, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !await ReworkAvailableAsync(ct)) throw new InvalidOperationException("REWORK_CALLER_TRANSACTION_REQUIRED");
        var json = JsonSerializer.Serialize(result, Json);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
            VALUES({actor},{result.OperationId},1,{fingerprint},1,{json},{result.OccurredAtUtc})
            """, ct);
        if (result.Action == "complete")
        {
            if (recipients.Length == 0 || result.PublicationId == null) throw new InvalidOperationException("MANDATORY_REWORK_NOTIFICATION_REQUIRED");
            var occurrence = $"REWORK:{result.Id}:SUBMITTED";
            var payload = JsonSerializer.Serialize(new { schemaVersion = 1, eventName = "REWORK_SUBMITTED", occurrenceKey = occurrence,
                sourceType = "CLEANING_REWORK", reworkId = result.Id, taskId = result.CleaningTaskId, taskCycleId = result.CleaningTaskId,
                roomId = room, result.BranchId, result.InspectionId, actorId = actor, result.OperationId, result.PublicationId,
                result.SourceETag, result.OccurredAtUtc, recipientCount = recipients.Length, sourceAuthority = "SYNTHETIC_TEST_ONLY" }, Json);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,rework_id,source_revision,payload_json,created_at,occurrence_key)
                VALUES({result.PublicationId},{actor},{result.OperationId},'REWORK_SUBMITTED',1,{result.Id},{Convert.FromHexString(result.SourceETag.Trim('"'))},{payload},{result.OccurredAtUtc},{occurrence})
                """, ct);
            foreach (var recipient in recipients)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.notification_delivery_intents(publication_id,user_id,delivery_channel,status,next_attempt_at) VALUES({result.PublicationId},{recipient},'IN_APP','PENDING',{result.OccurredAtUtc})", ct);
        }
        await VerifyReworkAsync(actor, result.OperationId, result.Action == "complete", ct);
    }

    public async Task VerifyReworkAsync(long actor, Guid operation, bool complete, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("CALLER_TRANSACTION_REQUIRED");
        var valid = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.notification_operation_reservations o
            JOIN dbo.audit_logs a ON a.actor_user_id=o.actor_user_id AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(a.new_values,'$.operationId'))=o.operation_key
            WHERE o.actor_user_id={actor} AND o.operation_key={operation} AND o.completed=1
            AND a.entity_type='cleaning_reworks' AND a.entity_id=JSON_VALUE(o.result_json,'$.id')
            AND JSON_VALUE(a.new_values,'$.entity.sourceETag')=JSON_VALUE(o.result_json,'$.sourceETag')
            AND ({complete}=0 OR EXISTS(SELECT 1 FROM dbo.notification_publications p
                WHERE p.actor_user_id=o.actor_user_id AND p.operation_key=o.operation_key AND p.event_type='REWORK_SUBMITTED' AND p.schema_version=1
                AND p.rework_id=TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.id'))
                AND p.publication_id=TRY_CONVERT(uniqueidentifier,JSON_VALUE(o.result_json,'$.publicationId'))
                AND JSON_VALUE(o.result_json,'$.sourceETag')=CHAR(34)+CONVERT(varchar(16),p.source_revision,2)+CHAR(34)
                AND TRY_CONVERT(int,JSON_VALUE(p.payload_json,'$.recipientCount'))>0
                AND (SELECT COUNT(*) FROM dbo.notification_delivery_intents i WHERE i.publication_id=p.publication_id)=TRY_CONVERT(int,JSON_VALUE(p.payload_json,'$.recipientCount'))))
            """).SingleAsync(ct);
        if (valid != 1) throw new InvalidOperationException("REWORK_DURABLE_PROOF_INCOMPLETE");
    }
}

public sealed partial class ProvisionalCleaningDelivery
{
    private sealed class ReworkIntent
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long BranchId { get; set; }
        public long ReworkId { get; set; }
        public long ActorId { get; set; }
    }
    private async Task<string> DeliverReworkOneAsync(CancellationToken ct, Guid? publication)
    {
        var selected = await db.Database.SqlQuery<ReworkIntent>($"""
            SELECT TOP(1) i.intent_id AS Id,i.user_id AS UserId,p.actor_user_id AS ActorId,p.rework_id AS ReworkId,
              TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.branchId')) AS BranchId
            FROM dbo.notification_delivery_intents i JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
            WHERE i.status='PENDING' AND i.next_attempt_at<=SYSUTCDATETIME() AND p.event_type='REWORK_SUBMITTED'
            AND ({publication} IS NULL OR p.publication_id={publication}) ORDER BY i.intent_id
            """).SingleOrDefaultAsync(ct);
        if (selected == null) return "EMPTY";
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await new UserEligibilityLock(db).LockAsync([selected.UserId], ct);
            if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.notification_delivery_intents WITH(UPDLOCK,HOLDLOCK) WHERE intent_id={selected.Id} AND status='PENDING'").SingleAsync(ct) != 1) return "EMPTY";
            if (!await candidate.ReworkAvailableAsync(ct) || !(await candidate.ClosureRecipientsAsync(selected.BranchId, ct)).Contains(selected.UserId)
                || !await db.CleaningReworks.AsNoTracking().AnyAsync(w => w.ReworkId == selected.ReworkId && w.CleaningTask.Room.BranchId == selected.BranchId
                    && w.Inspection.RoomId == w.CleaningTask.RoomId && w.Inspection.CleaningTaskId == w.CleaningTaskId, ct))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.notification_delivery_intents SET status='TERMINAL',failure_code='RECIPIENT_OR_SOURCE_DENIED',attempt_count=attempt_count+1 WHERE intent_id={selected.Id}", ct);
                await tx.CommitAsync(ct); return "RECIPIENT_OR_SOURCE_DENIED";
            }
            var notice = new Notification { NotificationType = "REWORK_SUBMITTED", Title = "Rework awaits reinspection",
                Message = "A rework has been submitted for reinspection.", Priority = "NORMAL", SourceType = "CLEANING_REWORK", SourceId = selected.ReworkId,
                ActionUrl = $"/cleaning/tasks/{selected.ReworkId}?branchId={selected.BranchId}&monitor=1&kind=rework",
                CreatedBy = selected.ActorId, CreatedAt = clock.GetUtcNow().UtcDateTime };
            db.Notifications.Add(notice); await db.SaveChangesAsync(ct);
            db.NotificationRecipients.Add(new() { NotificationId = notice.NotificationId, UserId = selected.UserId, DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = notice.CreatedAt });
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
