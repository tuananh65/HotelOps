using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;
public sealed record CleaningStoredOperation(string Fingerprint,bool Completed,string? ResultJson);
public interface ICleaningAssignmentStore
{
    Task EnsureAsync(CancellationToken ct);
    Task<CleaningStoredOperation?> LockOperationAsync(long actor,Guid operation,CancellationToken ct);
    Task<CleaningAssignmentState?> SourceAsync(long assignment,CancellationToken ct);
    Task<CleaningAssignmentGraph> GraphAsync(long actor,Guid operation,CancellationToken ct);
    Task<bool> CanReadAsync(CleaningAssignmentState source,long user,CancellationToken ct);
    Task InsertAsync(CleaningAssignmentCommand command,CleaningAssignmentEvent payload,CleaningAssignmentReceipt receipt,CancellationToken ct);
}
public sealed class CleaningAssignmentStore(HotelOpsDbContext db,TimeProvider clock,ICleaningCommonTargetAdmission? commonTarget=null):ICleaningAssignmentStore
{
    public async Task EnsureAsync(CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);
        var c=db.Database.GetDbConnection();
        if(c.DataSource!=IntegrationSqlGuard.Server||c.Database!=IntegrationSqlGuard.Database)
        {
            if(commonTarget is null)throw new NotificationContractUnavailableException();
            await commonTarget.RequireStoreAsync(db,ct);
            return; // Accepted observer verifies exact GUID/principal/catalog/source/transaction; no new connection.
        }
        var ok=await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='KIENPC'
              AND DB_NAME()='HotelOps_DEV2_FullStack_Trial' AND ORIGINAL_LOGIN()='HotelOps_DEV2_FullStack_Runtime'
              AND IS_SRVROLEMEMBER('sysadmin')=0 AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER')=0
              AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL')=0
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IntegrationTrial'
                AND CONVERT(nvarchar(128),value)='DEV2-INTEGRATION-02-SQL-PROVISIONAL')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS013.NotificationProposal')
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_operation_reservations')
                AND name='PK_notification_operation_v1' AND is_unique=1 AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_delivery_intents')
                AND name='UQ_delivery_publication_recipient_v1' AND is_unique=1 AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications')
                AND name IN('UX_fs009_publication_operation','UX_fe04_assignment_operation') AND is_unique=1 AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE name='TR_fs009_operation_immutable' AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE name='TR_fs009_publication_immutable' AND is_disabled=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct);
        if(ok!=1)throw new NotificationContractUnavailableException();
    }
    public async Task<CleaningStoredOperation?> LockOperationAsync(long actor,Guid operation,CancellationToken ct)
    {
        var row=await db.Database.SqlQuery<OperationRow>($"""
            SELECT fingerprint AS Fingerprint,completed AS Completed,result_json AS ResultJson
            FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK)
            WHERE actor_user_id={actor} AND operation_key={operation}
            """).SingleOrDefaultAsync(ct);
        return row is null?null:new(Convert.ToHexString(row.Fingerprint),row.Completed,row.ResultJson);
    }
    public Task<CleaningAssignmentState?> SourceAsync(long assignment,CancellationToken ct)=>db.Database.SqlQuery<CleaningAssignmentState>($"""
        SELECT a.assignment_id AS AssignmentId,a.cleaning_task_id AS TaskId,t.room_id AS RoomId,r.branch_id AS BranchId,
          a.assigned_by AS ActorId,a.assignee_user_id AS AssigneeId,a.assigned_at AS AssignedAt,a.ended_at AS EndedAt,
          a.assignment_status AS AssignmentStatus,t.workflow_status AS WorkflowState,t.status AS TaskStatus,
          t.due_at AS DueAt,COALESCE(c.cleaning_task_id,0) AS CurrentCycleId,CONVERT(varbinary(8),t.row_version) AS Revision,
          (SELECT COUNT(*) FROM dbo.cleaning_assignments other WITH(HOLDLOCK) WHERE other.cleaning_task_id=t.cleaning_task_id
            AND other.assignment_status='ACTIVE' AND other.ended_at IS NULL) AS ActiveAssignments,
          CONVERT(bit,CASE WHEN b.is_active=1 AND r.is_active=1 AND f.is_active=1 AND f.branch_id=r.branch_id
            AND (r.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas ar WITH(HOLDLOCK)
              WHERE ar.area_id=r.area_id AND ar.is_active=1 AND ar.branch_id=r.branch_id AND (ar.floor_id IS NULL OR ar.floor_id=r.floor_id))) THEN 1 ELSE 0 END) AS LocationActive
        FROM dbo.cleaning_assignments a WITH(HOLDLOCK)
        JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=a.cleaning_task_id
        JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=t.room_id
        JOIN dbo.floors f WITH(HOLDLOCK) ON f.floor_id=r.floor_id
        JOIN dbo.branches b WITH(HOLDLOCK) ON b.branch_id=r.branch_id
        LEFT JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=r.room_id
        WHERE a.assignment_id={assignment}
        """).SingleOrDefaultAsync(ct);
    public async Task<CleaningAssignmentGraph> GraphAsync(long actor,Guid operation,CancellationToken ct)
    {
        var rows=await db.Database.SqlQuery<GraphRow>($"""
            SELECT o.fingerprint AS Fingerprint,o.fingerprint_version AS FingerprintVersion,o.completed AS Completed,
              o.result_json AS ResultJson,p.publication_id AS PublicationId,p.event_type AS EventType,p.schema_version AS Version,
              p.assignment_id AS AssignmentId,p.source_revision AS SourceRevision,p.payload_json AS PayloadJson,
              (SELECT COUNT(*) FROM dbo.notification_publications px WITH(HOLDLOCK)
                WHERE px.actor_user_id=o.actor_user_id AND px.operation_key=o.operation_key) AS OperationPublications,
              CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.audit_logs a WITH(HOLDLOCK)
                JOIN dbo.cleaning_assignments ca WITH(HOLDLOCK) ON ca.assignment_id=p.assignment_id
                JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=ca.cleaning_task_id
                JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=t.room_id
                WHERE a.entity_type='cleaning_tasks' AND a.entity_id=CONVERT(nvarchar(30),ca.cleaning_task_id)
                  AND a.actor_user_id=o.actor_user_id AND a.branch_id=r.branch_id
                  AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.assignmentId'))=p.assignment_id
                  AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.operationId'))=o.operation_key)
                THEN 1 ELSE 0 END) AS HasAudit
            FROM dbo.notification_operation_reservations o WITH(HOLDLOCK)
            JOIN dbo.notification_publications p WITH(HOLDLOCK) ON p.actor_user_id=o.actor_user_id AND p.operation_key=o.operation_key
            WHERE o.actor_user_id={actor} AND o.operation_key={operation} AND p.event_type='CLEANING_TASK_ASSIGNED'
            """).ToArrayAsync(ct);
        if(rows.Length!=1)throw new NotificationConflictException();
        var r=rows[0];
        var recipients=await db.Database.SqlQuery<long>($"""
            SELECT user_id AS Value FROM dbo.notification_delivery_intents WITH(HOLDLOCK)
            WHERE publication_id={r.PublicationId} ORDER BY user_id
            """).ToArrayAsync(ct);
        var inApp=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_delivery_intents WITH(HOLDLOCK) WHERE publication_id={r.PublicationId} AND delivery_channel='IN_APP'").SingleAsync(ct);
        if(inApp!=recipients.Length||r.AssignmentId is null||r.ResultJson is null)throw new NotificationConflictException();
        return new(actor,operation,Convert.ToHexString(r.Fingerprint),r.FingerprintVersion,r.Completed,r.ResultJson,
            r.PublicationId,r.EventType,r.Version,r.AssignmentId.Value,r.SourceRevision,r.PayloadJson,r.HasAudit,Array.AsReadOnly(recipients),r.OperationPublications);
    }
    public async Task<bool> CanReadAsync(CleaningAssignmentState s,long user,CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        return s.AssigneeId==user&&s.AssignedAt<=now&&
            await db.UserBranchAccesses.AsNoTracking().AnyAsync(a=>a.UserId==user&&a.BranchId==s.BranchId&&a.RevokedAt==null&&a.GrantedAt<=now,ct)&&
            await NotificationCleaningReadQuery.Apply(db.CleaningAssignments.AsNoTracking(),db.Branches.AsNoTracking(),
                db.UserRoles.AsNoTracking(),db.RolePermissions.AsNoTracking(),db.UserBranchAccesses.AsNoTracking(),user,now)
                .AnyAsync(a=>a.AssignmentId==s.AssignmentId&&a.CleaningTaskId==s.TaskId&&a.CleaningTask.Room.BranchId==s.BranchId,ct);
    }
    public async Task InsertAsync(CleaningAssignmentCommand c,CleaningAssignmentEvent payload,CleaningAssignmentReceipt receipt,CancellationToken ct)
    {
        // Common target is native UC029 + receipt verification only; never widen direct EF staging.
        var connection=db.Database.GetDbConnection();
        if(connection.DataSource!=IntegrationSqlGuard.Server||connection.Database!=IntegrationSqlGuard.Database)
            throw new NotificationContractUnavailableException();
        // Same row shapes as DEV1 StageAsync. Caller supplies its own exact domain result.
        // No N01 metadata is written, no second reservation, no C04 intent, no SaveChanges/commit.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,completed,result_json,created_at)
            VALUES({c.ActorId},{c.OperationId},1,{Convert.FromHexString(c.RequestFingerprint)},1,{JsonSerializer.Serialize(receipt,CleaningAssignmentProfile.Json)},{payload.AssignedAtUtc})
            """,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,
              assignment_id,source_revision,payload_json,created_at)
            VALUES({payload.PublicationId},{c.ActorId},{c.OperationId},'CLEANING_TASK_ASSIGNED',1,{payload.AssignmentId},
              {Convert.FromHexString(payload.SourceRevision.Trim('"'))},{JsonSerializer.Serialize(payload,CleaningAssignmentProfile.Json)},{payload.AssignedAtUtc})
            """,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.notification_delivery_intents(publication_id,user_id,delivery_channel,status,next_attempt_at)
            VALUES({payload.PublicationId},{payload.AssigneeUserId},'IN_APP','PENDING',{payload.AssignedAtUtc})
            """,ct);
    }
    private sealed class OperationRow {public byte[] Fingerprint{get;set;}=[];public bool Completed{get;set;}public string? ResultJson{get;set;}}
    private sealed class GraphRow
    {public byte[] Fingerprint{get;set;}=[];public int FingerprintVersion{get;set;}public bool Completed{get;set;}public string? ResultJson{get;set;}
     public Guid PublicationId{get;set;}public string EventType{get;set;}="";public int Version{get;set;}public long? AssignmentId{get;set;}
     public byte[] SourceRevision{get;set;}=[];public string PayloadJson{get;set;}="";public bool HasAudit{get;set;}public int OperationPublications{get;set;}}
}
