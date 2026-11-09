using System.Data;
using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Housekeeping;

public sealed record InspectionProjectionFacts(string InspectionState, string InspectionStatus, string? Result,
    string CleaningState, string CleaningStatus, string OperationalStatus, bool ActiveRoom, bool CurrentCycle,
    bool RequiredCriteriaPassed, bool HasFailedCriterion, bool HasOpenRework, bool TrustedConclusionReceipt, bool EffectiveBlock);
public sealed record InspectionRoomProjection(string OperationalStatus, string Readiness, bool EffectiveBlock)
{
    public string EffectiveReadiness => EffectiveBlock ? "BLOCKED" : Readiness;
}

public static class InspectionRoomProjectionRules
{
    // Rework Required / Blocked are readiness overlays, never invented values in rooms.operational_status.
    public static InspectionRoomProjection Evaluate(InspectionProjectionFacts source)
    {
        if (!source.ActiveRoom || !source.CurrentCycle || !source.TrustedConclusionReceipt
            || source.InspectionStatus != "COMPLETED") throw new InvalidOperationException("INSPECTION_PROJECTION_AUTHORITY_REQUIRED");
        if (source.OperationalStatus is not ("AVAILABLE" or "OCCUPIED" or "DIRTY" or "CLEANING" or "INSPECTION" or "READY" or "OUT_OF_SERVICE"))
            throw new InvalidOperationException("ROOM_OPERATIONAL_SCHEMA_INCOMPATIBLE");
        if (!source.EffectiveBlock && source.OperationalStatus is "OCCUPIED" or "OUT_OF_SERVICE")
            throw new InvalidOperationException("ROOM_OPERATIONAL_CONFLICT");
        if (source.InspectionState == "PASSED" && source.Result == "PASS" && source.CleaningState == "COMPLETED"
            && source.CleaningStatus == "COMPLETED" && source.RequiredCriteriaPassed && !source.HasOpenRework)
            return new(source.EffectiveBlock ? source.OperationalStatus : "READY","READY",source.EffectiveBlock);
        if (source.InspectionState == "FAILED" && source.Result == "FAIL" && source.CleaningState == "REWORK_REQUIRED"
            && source.CleaningStatus == "IN_PROGRESS" && source.HasFailedCriterion)
            return new(source.OperationalStatus,"REWORK_REQUIRED",source.EffectiveBlock);
        throw new InvalidOperationException("INSPECTION_PROJECTION_POSTCONDITION_INVALID");
    }
}

// Trusted FE05 participant only. Caller authorizes/locks source and stages mandatory notifications;
// this adapter verifies persisted postconditions/receipt, projects in the SAME transaction and never commits.
public sealed class InspectionRoomProjectionAdapter(HotelOpsDbContext db)
{
    private sealed class ProjectionRow
    {
        public long RoomId { get; set; }
        public long CleaningTaskId { get; set; }
        public string InspectionState { get; set; } = "";
        public string InspectionStatus { get; set; } = "";
        public string? Result { get; set; }
        public string CleaningState { get; set; } = "";
        public string CleaningStatus { get; set; } = "";
        public string OperationalStatus { get; set; } = "";
        public bool ActiveRoom { get; set; }
        public bool CurrentCycle { get; set; }
        public bool RequiredCriteriaPassed { get; set; }
        public bool HasFailedCriterion { get; set; }
        public bool HasOpenRework { get; set; }
        public bool TrustedConclusionReceipt { get; set; }
        public bool EffectiveBlock { get; set; }
    }

    public async Task<InspectionRoomProjection> ApplyAsync(DbConnection connection,DbTransaction transaction,
        long actor,long branch,long inspection,byte[] expectedRevision,Guid operation,CancellationToken ct)
    {
        RequireTransaction(connection,transaction);
        if (actor<=0 || branch<=0 || inspection<=0 || expectedRevision.Length!=8 || operation==Guid.Empty)
            throw new ArgumentException("Persisted conclusion identity and revision required.");
        // Required only for READY readiness, which the existing FS016 CK deliberately does not allow.
        if (await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.FE04.InspectionProjection.V1' AND CONVERT(nvarchar(128),value)='READINESS_OVERLAY_V1')
              AND OBJECT_ID('dbo.fe04_rework_authority_issuers','U') IS NOT NULL THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1) throw new InvalidOperationException("INSPECTION_PROJECTION_SCHEMA_REQUIRED");
        var row=await db.Database.SqlQuery<ProjectionRow>($"""
            SELECT i.room_id AS RoomId,t.cleaning_task_id AS CleaningTaskId,i.workflow_status AS InspectionState,
              i.status AS InspectionStatus,i.result AS Result,t.workflow_status AS CleaningState,t.status AS CleaningStatus,
              r.operational_status AS OperationalStatus,CONVERT(bit,CASE WHEN r.is_active=1 AND f.is_active=1
                AND (r.area_id IS NULL OR a.is_active=1) THEN 1 ELSE 0 END) AS ActiveRoom,
              CONVERT(bit,CASE WHEN c.cleaning_task_id=t.cleaning_task_id
                AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections n WITH(HOLDLOCK)
                  WHERE n.cleaning_task_id=t.cleaning_task_id AND n.inspection_id>i.inspection_id) THEN 1 ELSE 0 END) AS CurrentCycle,
              CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.inspection_checklist_items ci WITH(HOLDLOCK)
                  WHERE ci.version_id=i.version_id AND ci.is_required=1 AND ci.is_active=1)
                AND NOT EXISTS(SELECT 1 FROM dbo.inspection_checklist_items ci WITH(HOLDLOCK)
                  WHERE ci.version_id=i.version_id AND ci.is_required=1 AND ci.is_active=1
                  AND NOT EXISTS(SELECT 1 FROM dbo.inspection_results ir WITH(HOLDLOCK)
                    WHERE ir.inspection_id=i.inspection_id AND ir.checklist_item_id=ci.item_id AND ir.outcome='PASS'))
                AND NOT EXISTS(SELECT 1 FROM dbo.inspection_results ir WITH(HOLDLOCK)
                  JOIN dbo.inspection_checklist_items ci WITH(HOLDLOCK) ON ci.item_id=ir.checklist_item_id
                  WHERE ir.inspection_id=i.inspection_id AND ci.version_id=i.version_id
                    AND ci.is_required=1 AND ci.is_active=1 AND ir.outcome<>'PASS')
                THEN 1 ELSE 0 END) AS RequiredCriteriaPassed,
              CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.inspection_results ir WITH(HOLDLOCK)
                JOIN dbo.inspection_checklist_items ci WITH(HOLDLOCK) ON ci.item_id=ir.checklist_item_id
                WHERE ir.inspection_id=i.inspection_id AND ci.version_id=i.version_id AND ir.outcome='FAIL') THEN 1 ELSE 0 END) AS HasFailedCriterion,
              CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.cleaning_reworks w WITH(HOLDLOCK)
                WHERE w.cleaning_task_id=t.cleaning_task_id AND w.status NOT IN('COMPLETED','CANCELLED')) THEN 1 ELSE 0 END) AS HasOpenRework,
              CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.notification_operation_reservations o WITH(HOLDLOCK)
                WHERE o.actor_user_id={actor} AND o.operation_key={operation} AND o.completed=1 AND ISJSON(o.result_json)=1
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.contract')='FE05_INSPECTION_CONCLUSION_V1'
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.issuer')='FE05.UC044.UC045'
                AND TRY_CONVERT(int,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.issuerVersion'))=1
                AND EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x WITH(HOLDLOCK)
                  JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
                  WHERE x.issuer='FE05.UC044.UC045' AND x.version=1 AND x.is_active=1
                  AND m.execute_as_principal_id=-2 AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest)
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.inspectionId'))=i.inspection_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.branchId'))=r.branch_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.cleaningTaskId'))=t.cleaning_task_id
                AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.roomId'))=r.room_id
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.inspectionETag')=CHAR(34)+CONVERT(varchar(16),i.row_version,2)+CHAR(34)
                AND JSON_VALUE(CASE WHEN ISJSON(o.result_json)=1 THEN o.result_json ELSE N'[]' END,'$.conclusion')=i.workflow_status) THEN 1 ELSE 0 END) AS TrustedConclusionReceipt,
              CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.room_blocks b WITH(UPDLOCK,HOLDLOCK)
                WHERE b.room_id=r.room_id AND b.blocked_at<=SYSUTCDATETIME()
                AND (b.released_at IS NULL OR b.released_at>SYSUTCDATETIME())) THEN 1 ELSE 0 END) AS EffectiveBlock
            FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r WITH(UPDLOCK,HOLDLOCK) ON r.room_id=i.room_id
            JOIN dbo.floors f WITH(HOLDLOCK) ON f.floor_id=r.floor_id AND f.branch_id=r.branch_id
            LEFT JOIN dbo.areas a WITH(HOLDLOCK) ON a.area_id=r.area_id AND a.branch_id=r.branch_id
            LEFT JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=r.room_id
            WHERE i.inspection_id={inspection} AND r.branch_id={branch} AND i.inspector_user_id={actor}
              AND i.row_version={expectedRevision} AND i.inspected_at IS NOT NULL
            """).SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("INSPECTION_PROJECTION_STALE_SOURCE");
        var projection=InspectionRoomProjectionRules.Evaluate(new(row.InspectionState,row.InspectionStatus,row.Result,
            row.CleaningState,row.CleaningStatus,row.OperationalStatus,row.ActiveRoom,row.CurrentCycle,
            row.RequiredCriteriaPassed,row.HasFailedCriterion,row.HasOpenRework,row.TrustedConclusionReceipt,row.EffectiveBlock));
        var audit=JsonSerializer.Serialize(new { projectionOperation=operation, inspectionId=inspection,
            row.CleaningTaskId, operationalStatus=projection.OperationalStatus, readiness=projection.Readiness,
            effectiveBlock=projection.EffectiveBlock, authorityContract="FE05_INSPECTION_CONCLUSION_V1" });
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET XACT_ABORT ON;
            IF EXISTS(SELECT 1 FROM dbo.audit_logs WITH(UPDLOCK,HOLDLOCK)
              WHERE entity_type='rooms' AND TRY_CONVERT(bigint,entity_id)={row.RoomId}
              AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.projectionOperation'))={operation})
              THROW 51993,'Projection already applied; caller must use its durable replay receipt',1;
            UPDATE dbo.rooms SET operational_status={projection.OperationalStatus}
              WHERE room_id={row.RoomId} AND branch_id={branch} AND operational_status={row.OperationalStatus};
            IF @@ROWCOUNT<>1 THROW 51993,'ROOM_PROJECTION_CONFLICT',1;
            INSERT dbo.room_readiness_events(room_id,cleaning_task_id,readiness,has_effective_block,actor_user_id,occurred_at)
              VALUES({row.RoomId},{row.CleaningTaskId},{projection.Readiness},{projection.EffectiveBlock},{actor},SYSUTCDATETIME());
            IF {row.OperationalStatus}<>{projection.OperationalStatus}
              INSERT dbo.room_status_history(room_id,from_status,to_status,changed_by,changed_at,reason)
              VALUES({row.RoomId},{row.OperationalStatus},{projection.OperationalStatus},{actor},SYSUTCDATETIME(),N'Authoritative Inspection projection');
            INSERT dbo.audit_logs(branch_id,actor_user_id,entity_type,entity_id,action,new_values,occurred_at)
              VALUES({branch},{actor},'rooms',CONVERT(nvarchar(100),{row.RoomId}),'UPDATE',{audit},SYSUTCDATETIME());
            """,ct);
        return projection;
    }

    private void RequireTransaction(DbConnection connection,DbTransaction transaction)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not {} tx
            || !ReferenceEquals(connection,db.Database.GetDbConnection()) || !ReferenceEquals(transaction,tx.GetDbTransaction())
            || !ReferenceEquals(transaction.Connection,connection) || transaction.IsolationLevel!=IsolationLevel.Serializable
            || db.ChangeTracker.Entries().Any() || System.Transactions.Transaction.Current!=null)
            throw new InvalidOperationException("INSPECTION_PROJECTION_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED");
    }
}
