using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// Extends the existing publisher and physical operation/publication/intent ledger.
public sealed partial class ProvisionalCleaningNotification
{
    public async Task<bool> CancellationAvailableAsync(CancellationToken ct)
    {
        if (!env.IsDevelopment() || config["CleaningCancellation:Candidate"] != "FS018_PROVISIONAL"
            || db.Database.GetDbConnection().Database != "HotelOps_Canonical_FS018_Cancellation_V1_Fresh") return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME()
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS018.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS018')
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS018.Cancellation' AND CONVERT(nvarchar(128),value)='PROVISIONAL_NOT_ACCEPTED')
            AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_fs018_publication_event' AND is_disabled=0 AND is_not_trusted=0)
            AND OBJECT_ID('dbo.notification_delivery_intents','U') IS NOT NULL THEN 1 ELSE 0 END AS [Value]
            """).SingleAsync(ct) == 1;
    }

    public async Task StageCancellationAsync(long actor, byte[] fingerprint, CleaningCancellationResult r, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !await CancellationAvailableAsync(ct))
            throw new InvalidOperationException("CANCELLATION_STAGING_UNAVAILABLE");
        var json = JsonSerializer.Serialize(r, Json);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
            VALUES({actor},{r.OperationId},1,{fingerprint},1,{json},{r.OccurredAtUtc});
            """, ct);
        if (r.PublicationId is { } publication)
        {
            var occurrence = $"CLEANING:{r.Id}:CANCELLED";
            var payload = JsonSerializer.Serialize(new { schemaVersion = 1, eventName = "CLEANING_TASK_CANCELLED",
                occurrenceKey = occurrence, taskId = r.Id, taskCycleId = r.Id, r.RoomId, r.BranchId,
                r.AssignmentId, r.AssigneeId, actorId = actor, r.OperationId, r.OccurredAtUtc, r.SourceETag }, Json);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,assignment_id,source_revision,payload_json,created_at,occurrence_key)
                VALUES({publication},{actor},{r.OperationId},'CLEANING_TASK_CANCELLED',1,{r.AssignmentId},{Convert.FromHexString(r.SourceETag.Trim('"'))},{payload},{r.OccurredAtUtc},{occurrence});
                INSERT dbo.notification_delivery_intents(publication_id,user_id,delivery_channel,status,next_attempt_at)
                VALUES({publication},{r.AssigneeId},'IN_APP','PENDING',{r.OccurredAtUtc});
                """, ct);
        }
        await VerifyCancellationAsync(actor, r, ct);
    }

    public async Task VerifyCancellationAsync(long actor, CleaningCancellationResult r, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("CALLER_TRANSACTION_REQUIRED");
        var proof = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.notification_operation_reservations o
            WHERE o.actor_user_id={actor} AND o.operation_key={r.OperationId} AND o.completed=1 AND o.fingerprint_version=1
            AND EXISTS(SELECT 1 FROM dbo.cleaning_tasks t WHERE t.cleaning_task_id={r.Id} AND t.room_id={r.RoomId} AND t.workflow_status='CANCELLED' AND t.status='CANCELLED')
            AND EXISTS(SELECT 1 FROM dbo.rooms room WHERE room.room_id={r.RoomId} AND room.branch_id={r.BranchId})
            AND EXISTS(SELECT 1 FROM dbo.room_readiness_events e WHERE e.event_id={r.ReadinessEventId} AND e.room_id={r.RoomId} AND e.cleaning_task_id={r.Id} AND e.readiness='PENDING_CLEANING' AND e.has_effective_block=0 AND e.actor_user_id={actor} AND e.occurred_at={r.OccurredAtUtc})
            AND EXISTS(SELECT 1 FROM dbo.audit_logs a WHERE a.actor_user_id={actor} AND a.entity_type='cleaning_tasks' AND a.entity_id=CONVERT(varchar(30),{r.Id}) AND a.branch_id={r.BranchId} AND JSON_VALUE(a.new_values,'$.action')='CANCEL_CLEANING' AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(a.new_values,'$.operationId'))={r.OperationId})
            AND (({r.AssignmentId} IS NULL AND {r.PublicationId} IS NULL AND NOT EXISTS(SELECT 1 FROM dbo.notification_publications p WHERE p.actor_user_id={actor} AND p.operation_key={r.OperationId}))
              OR EXISTS(SELECT 1 FROM dbo.notification_publications p JOIN dbo.cleaning_assignments s ON s.assignment_id=p.assignment_id
                WHERE p.publication_id={r.PublicationId} AND p.actor_user_id={actor} AND p.operation_key={r.OperationId}
                AND p.event_type='CLEANING_TASK_CANCELLED' AND p.schema_version=1 AND p.rework_id IS NULL
                AND s.assignment_id={r.AssignmentId} AND s.cleaning_task_id={r.Id} AND s.assignee_user_id={r.AssigneeId} AND s.assignment_status='CANCELLED' AND s.ended_at={r.OccurredAtUtc}
                AND p.source_revision={Convert.FromHexString(r.SourceETag.Trim('"'))}
                AND JSON_VALUE(p.payload_json,'$.sourceETag')={r.SourceETag}
                AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.taskId'))={r.Id}
                AND TRY_CONVERT(bigint,JSON_VALUE(p.payload_json,'$.branchId'))={r.BranchId}
                AND (SELECT COUNT(*) FROM dbo.notification_delivery_intents i WHERE i.publication_id=p.publication_id)=1
                AND EXISTS(SELECT 1 FROM dbo.notification_delivery_intents i WHERE i.publication_id=p.publication_id AND i.user_id={r.AssigneeId} AND i.delivery_channel='IN_APP')))
            """).SingleAsync(ct);
        if (proof != 1) throw new InvalidOperationException("MANDATORY_CANCELLATION_PROOF_INCOMPLETE");
    }
}
