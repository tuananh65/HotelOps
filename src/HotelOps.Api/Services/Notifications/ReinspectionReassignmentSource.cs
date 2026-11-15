using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal static class ReinspectionReassignmentSource
{
    internal const string Kind = "REINSPECTION_ASSIGNED";
    internal const string Contract = "FE05_REINSPECTION_REASSIGNMENT_V1";
    internal sealed record Receipt(string Contract, string Issuer, int IssuerVersion, long ActorUserId, long BranchId, long InspectionId,
        Guid OperationId, long WriterTransactionId, long AuditId, long ReworkId, long OriginalInspectorId, ReinspectionReassignmentResult Entity, ReworkAssignmentIntent Notification);
    internal static bool ValidResult(ReinspectionReassignmentResult? r) => r is { Id: > 0, TaskId: > 0, BranchId: > 0, RoomId: > 0,
        VersionId: > 0, PreviousInspectorId: > 0, InspectorId: > 0, State: "ASSIGNED", ReworkId: > 0, Notification: "PENDING_DELIVERY", Contract: Contract }
        && StrongTag(r.ReworkETag) && StrongTag(r.BindingETag) && r.PreviousInspectorId != r.InspectorId && r.OperationId != Guid.Empty && r.SourceETag is { Length: 18 } tag && tag[0] == '"' && tag[^1] == '"'
        && tag.Skip(1).Take(16).All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static bool StrongTag(string? tag) => tag is {Length:18} && tag[0]=='"' && tag[^1]=='"' && tag.Skip(1).Take(16).All(c=>c is >= '0' and <= '9' or >= 'A' and <= 'F');
    internal static Receipt? Parse(string? json)
    {
        try
        {
            var r = json == null ? null : JsonSerializer.Deserialize<Receipt>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return r is { Contract: Contract, Issuer: "FE05.UC041.REINSPECTION_REASSIGN", IssuerVersion: 1, ActorUserId: > 0, BranchId: > 0,
                InspectionId: > 0, WriterTransactionId: > 0, AuditId: > 0, ReworkId: > 0, OriginalInspectorId: > 0, Notification: { Kind: Kind, SchemaVersion: 1, RecipientUserId: > 0 } }
                && ValidResult(r.Entity) && r.Entity.Id == r.InspectionId && r.Entity.BranchId == r.BranchId
                && r.Entity.ReworkId == r.ReworkId && r.Entity.OperationId == r.OperationId && r.Entity.InspectorId == r.Notification.RecipientUserId ? r : null;
        }
        catch (JsonException) { return null; }
    }
    internal static async Task<Receipt?> VerifyAsync(HotelOpsDbContext db, AuditLog audit, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("NOTIFICATION_SOURCE_SERIALIZABLE_REQUIRED");
        var r = Parse(audit.NewValues);
        if (r == null || audit.AuditId != r.AuditId || audit.EntityType != "REINSPECTION_ASSIGNMENT" || audit.Action != "UPDATE" || audit.ActorUserId != r.ActorUserId
            || audit.BranchId != r.BranchId || audit.EntityId != r.InspectionId.ToString()) return null;
        if (await db.Database.SqlQueryRaw<int>("SELECT CASE WHEN OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL AND OBJECT_ID('dbo.fe05_reassign_reinspection','P') IS NOT NULL AND OBJECT_ID('dbo.fe05_reinspection_bindings_v1','U') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(ct) != 1) return null;
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations o WITH(HOLDLOCK)
            JOIN dbo.fe04_rework_authority_issuers x ON x.issuer='FE05.UC041.REINSPECTION_REASSIGN' AND x.version=1 AND x.is_active=1
            JOIN sys.sql_modules m ON m.object_id=x.writer_object_id AND m.execute_as_principal_id=-2
            JOIN dbo.room_inspections i ON i.inspection_id={r.InspectionId} AND i.cleaning_task_id={r.Entity.TaskId}
              AND i.room_id={r.Entity.RoomId} AND i.version_id={r.Entity.VersionId}
            JOIN dbo.fe05_reinspection_bindings_v1 b ON b.inspection_id=i.inspection_id AND b.rework_id={r.ReworkId}
              AND b.branch_id={r.BranchId} AND b.room_id=i.room_id AND b.version_id=i.version_id AND b.cleaning_task_id=i.cleaning_task_id
              AND b.reinspector_user_id={r.OriginalInspectorId}
            JOIN dbo.rooms room ON room.room_id=i.room_id AND room.branch_id={r.BranchId} AND room.is_active=1
            JOIN dbo.floors f ON f.floor_id=room.floor_id AND f.is_active=1 LEFT JOIN dbo.areas a ON a.area_id=room.area_id
            WHERE o.actor_user_id={r.ActorUserId} AND o.operation_key={r.OperationId} AND o.completed=1 AND o.fingerprint_version=1
              AND (room.area_id IS NULL OR a.is_active=1) AND {r.WriterTransactionId}<>CURRENT_TRANSACTION_ID()
              AND x.writer_object_id=OBJECT_ID('dbo.fe05_reassign_reinspection') AND x.source_commit LIKE REPLICATE('[0-9a-f]',40)
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),o.result_json))=HASHBYTES('SHA2_256',CONVERT(varbinary(max),{audit.NewValues}))
              AND (SELECT COUNT(*) FROM dbo.audit_logs WHERE entity_type='REINSPECTION_ASSIGNMENT' AND action='UPDATE'
                AND entity_id=CONVERT(nvarchar(50),i.inspection_id) AND branch_id={r.BranchId} AND actor_user_id={r.ActorUserId}
                AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),new_values))=HASHBYTES('SHA2_256',CONVERT(varbinary(max),{audit.NewValues})))=1
            """).SingleAsync(ct) == 1 ? r : null;
    }
}
