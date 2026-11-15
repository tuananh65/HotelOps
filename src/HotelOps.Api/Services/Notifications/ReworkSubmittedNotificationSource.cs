using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// Reuses the committed submission intent for delivery and current source navigation.
internal static class ReworkSubmittedNotificationSource
{
    internal const string Kind = "REWORK_SUBMITTED";
    internal static async Task<string> VerifyAsync(HotelOpsDbContext db, AuditLog audit, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("NOTIFICATION_SOURCE_SERIALIZABLE_REQUIRED");
        var branch = audit.BranchId;
        if(audit is not {EntityType:"cleaning_reworks",Action:"UPDATE",ActorUserId:>0}||audit.BranchId is not >0
            ||!long.TryParse(audit.EntityId,out var rework)||rework<=0||audit.NewValues==null)return "SOURCE_DENIED";
        if(await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN OBJECT_ID('dbo.fe04_commit_rework_submission','P') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.ReworkSubmission.V1'
                AND CONVERT(nvarchar(128),value)='FE04_REWORK_SUBMISSION_V1') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1)return "SOURCE_UNAVAILABLE";
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations o WITH(HOLDLOCK)
            CROSS APPLY(SELECT CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END AS receipt) j
            JOIN dbo.cleaning_reworks w WITH(HOLDLOCK) ON w.rework_id={rework} AND w.assignee_user_id=o.actor_user_id
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=w.cleaning_task_id
            JOIN dbo.rooms r ON r.room_id=t.room_id AND r.branch_id={branch} AND r.is_active=1
            JOIN dbo.floors f ON f.floor_id=r.floor_id AND f.is_active=1
            LEFT JOIN dbo.areas a ON a.area_id=r.area_id
            JOIN dbo.room_cleaning_cycle_state c ON c.room_id=r.room_id AND c.cleaning_task_id=t.cleaning_task_id
            JOIN dbo.fe04_rework_authority_issuers x ON x.issuer='FE04.UC036' AND x.version=1 AND x.is_active=1
            JOIN sys.sql_modules m ON m.object_id=x.writer_object_id AND m.execute_as_principal_id=-2
            WHERE (r.area_id IS NULL OR a.is_active=1) AND o.actor_user_id={audit.ActorUserId} AND o.completed=1 AND o.fingerprint_version=1
              AND x.writer_object_id=OBJECT_ID('dbo.fe04_commit_rework_submission') AND x.source_commit LIKE REPLICATE('[0-9a-f]',40)
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),o.result_json))=HASHBYTES('SHA2_256',CONVERT(varbinary(max),{audit.NewValues}))
              AND w.workflow_status='PENDING_REINSPECTION' AND w.status='IN_PROGRESS' AND w.completed_at IS NULL
              AND JSON_VALUE(j.receipt,'$.contract')='FE04_REWORK_SUBMISSION_V1'
              AND JSON_VALUE(j.receipt,'$.issuer')='FE04.UC036' AND JSON_VALUE(j.receipt,'$.issuerVersion')='1'
              AND JSON_VALUE(j.receipt,'$.notification.kind')={Kind} AND JSON_VALUE(j.receipt,'$.notification.schemaVersion')='1'
              AND JSON_VALUE(j.receipt,'$.notification.audience')='BRANCH_HK_SUPERVISORS'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.actorUserId'))=o.actor_user_id
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.branchId'))=r.branch_id
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.reworkId'))=w.rework_id
              AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(j.receipt,'$.operationId'))=o.operation_key
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.writerTransactionId'))>0
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.writerTransactionId'))<>CURRENT_TRANSACTION_ID()
              AND JSON_VALUE(j.receipt,'$.entity.state')='PENDING_REINSPECTION'
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.entity.inspectionId'))=w.inspection_id
              AND TRY_CONVERT(bigint,JSON_VALUE(j.receipt,'$.entity.cleaningTaskId'))=t.cleaning_task_id
            """).SingleAsync(ct)!=1)return "SOURCE_DENIED";
        return "VALID";
    }
}
