using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.HotelStructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// A conclusion event proves a historical occurrence, never current room-use authority.
internal static class ReinspectionPassedNotificationSource
{
    internal const string Kind="REINSPECTION_PASSED";
    private const string Contract="FE05_REINSPECTION_CONCLUSION_V1";
    internal sealed record Receipt(string Contract,string Issuer,int IssuerVersion,long ActorUserId,long BranchId,
        long InspectionId,long ReworkId,Guid OperationId,long WriterTransactionId,ReinspectionConclusionResult Entity,
        ReworkAssignmentIntent Notification);
    internal static Receipt? Parse(string? json)
    {
        if(json==null)return null;
        try
        {
            var r=JsonSerializer.Deserialize<Receipt>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return r is {Contract:Contract,Issuer:"FE05.UC047",IssuerVersion:1,ActorUserId:>0,BranchId:>0,WriterTransactionId:>0,
                Notification:{SchemaVersion:1,Kind:Kind,RecipientUserId:>0}}
                &&r.Entity is {State:"PASSED"}&&RoomTypeCreationService.ValidReinspectionConclusionReceipt(r.Entity,r.BranchId,r.InspectionId,r.ReworkId,r.OperationId)?r:null;
        }
        catch(JsonException){return null;}
    }

    // Caller holds a serializable transaction and performs fresh recipient authorization.
    internal static async Task<Receipt?> VerifyAsync(HotelOpsDbContext db,AuditLog audit,CancellationToken ct)
    {
        var r=Parse(audit.NewValues);
        if(r==null||audit.EntityType!="REINSPECTION"||audit.Action!="UPDATE"||audit.ActorUserId!=r.ActorUserId
            ||audit.BranchId!=r.BranchId||audit.EntityId!=r.InspectionId.ToString())return null;
        if(await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN OBJECT_ID('dbo.fe05_reinspection_bindings_v1','U') IS NOT NULL
              AND OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE name='TR_reinspection_binding_immutable_v1' AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Reinspection.V1'
                AND CONVERT(nvarchar(128),value)='FE05_REINSPECTION_V1') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1)return null;
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fe05_reinspection_bindings_v1 b WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.room_inspections i ON i.inspection_id=b.inspection_id AND i.inspector_user_id=b.reinspector_user_id
              AND i.cleaning_task_id=b.cleaning_task_id AND i.room_id=b.room_id AND i.version_id=b.version_id
            JOIN dbo.cleaning_reworks w ON w.rework_id=b.rework_id AND w.inspection_id=b.original_inspection_id
              AND w.cleaning_task_id=b.cleaning_task_id AND w.assignee_user_id=b.submission_actor
            JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=b.cleaning_task_id AND t.room_id=b.room_id
            JOIN dbo.rooms room ON room.room_id=b.room_id AND room.branch_id=b.branch_id AND room.is_active=1
            JOIN dbo.floors f ON f.floor_id=room.floor_id AND f.is_active=1
            LEFT JOIN dbo.areas area ON area.area_id=room.area_id
            JOIN dbo.fe04_rework_authority_issuers x ON x.issuer='FE05.UC047' AND x.version=1
            JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
            JOIN dbo.notification_operation_reservations o ON o.actor_user_id={r.ActorUserId} AND o.operation_key={r.OperationId}
            WHERE b.inspection_id={r.InspectionId} AND b.rework_id={r.ReworkId} AND b.branch_id={r.BranchId}
              AND b.reinspector_user_id={r.ActorUserId} AND b.submission_actor={r.Notification.RecipientUserId}
              AND b.submission_actor<>b.reinspector_user_id AND b.room_id={r.Entity.RoomId} AND b.cleaning_task_id={r.Entity.CleaningTaskId}
              AND (room.area_id IS NULL OR area.is_active=1)
              AND i.workflow_status='PASSED' AND i.status='COMPLETED' AND i.result='PASS' AND i.inspected_at IS NOT NULL
              AND w.workflow_status='COMPLETED' AND w.status='COMPLETED' AND w.completed_at=i.inspected_at
              AND i.row_version=CONVERT(binary(8),REPLACE({r.Entity.SourceETag},'"',''),2)
              AND w.row_version=CONVERT(binary(8),REPLACE({r.Entity.ReworkETag},'"',''),2)
              AND x.is_active=1 AND x.writer_object_id=OBJECT_ID('dbo.fe05_commit_reinspection')
              AND x.source_commit LIKE REPLICATE('[0-9a-f]',40) AND m.execute_as_principal_id=-2
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
              AND o.completed=1 AND o.fingerprint_version=1 AND {r.WriterTransactionId}<>CURRENT_TRANSACTION_ID()
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),o.result_json))=HASHBYTES('SHA2_256',CONVERT(varbinary(max),{audit.NewValues}))
              AND(SELECT COUNT(*) FROM dbo.audit_logs a WHERE a.entity_type='REINSPECTION' AND a.action='UPDATE'
                AND a.entity_id={audit.EntityId} AND a.actor_user_id={r.ActorUserId} AND a.branch_id={r.BranchId}
                AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),a.new_values))=HASHBYTES('SHA2_256',CONVERT(varbinary(max),o.result_json)))=1
            """).SingleAsync(ct)==1?r:null;
    }
}
