using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace HotelOps.Api.Services.HotelStructure;

// Scoped retained caller facts, not a persisted proof or replacement SQL authority.
public sealed class InspectionOwnerOperation
{
    internal SessionReference? Ticket {get;private set;}
    internal long SessionId {get;private set;}
    internal Fe05OperationRequest? Request {get;private set;}
    internal Guid TransactionId {get;private set;}
    private System.Data.Common.DbTransaction? physical;
    internal string? WrittenRevision {get;private set;}
    internal long? ReworkId {get;private set;}
    internal Func<long,string,string,CancellationToken,Task>? VerifySnapshot {get;set;}
    internal void Bind(HotelOpsDbContext db,SessionReference ticket,long sid,Fe05OperationRequest request)
    {
        Fe05CallerTransaction.Require(db);
        if(Request!=null)throw new Fe05NotificationUnavailableException("FE05_OWNER_OPERATION_ALREADY_BOUND");
        Ticket=ticket;SessionId=sid;Request=request with{RequiredEvents=Array.AsReadOnly(request.RequiredEvents.ToArray())};
        TransactionId=db.Database.CurrentTransaction!.TransactionId;physical=db.Database.CurrentTransaction.GetDbTransaction();
    }
    internal void Require(HotelOpsDbContext db,Fe05OperationRequest request,CurrentUserInfo actor)
    {
        Fe05CallerTransaction.Require(db,db.Database.GetDbConnection(),physical);
        if(Ticket==null||Request==null||actor.IsRestricted||actor.UserId!=Ticket.UserId||actor.SessionId!=SessionId
            ||TransactionId!=db.Database.CurrentTransaction!.TransactionId||!ReferenceEquals(physical,db.Database.CurrentTransaction.GetDbTransaction())
            ||Request.OperationId!=request.OperationId||Request.BranchId!=request.BranchId||Request.InspectionId!=request.InspectionId
            ||Request.RequestFingerprint!=request.RequestFingerprint||!Request.RequiredEvents.SequenceEqual(request.RequiredEvents))
            throw new Fe05NotificationUnavailableException("FE05_OWNER_OPERATION_MISMATCH");
    }
    internal void Written(string revision,long? rework=null){WrittenRevision=revision;ReworkId=rework;}
}

// Explicit isolated candidate only. Program/default registration remains unaccepted.
public sealed class InspectionOwnerSourceCoordination(HotelOpsDbContext context,ICurrentUserAccessor current,
    ISqlSessionService sessions,IPermissionService permissions,InspectionOwnerOperation owner,InspectionUsersPrelock users)
    : IFe05SourceCoordination
{
    public async Task RequireReservationAsync(HotelOpsDbContext db,Fe05OperationRequest request,CurrentUserInfo actor,CancellationToken ct)
    {
        await Authorize(db,request,actor,ct);
        await users.ConfirmAfterSourcesAsync(false,ct);
        if(request.RequiredEvents.Contains(Fe05NotificationKind.InspectionFailed)&&users.Roster.CleanerBinding==null)
            throw new Fe05NotificationUnavailableException("FE04_R05_COMPLETE_ATTRIBUTION_BINDING_REQUIRED");
        if(await db.Database.SqlQuery<string>($"SELECT APPLOCK_MODE('public','HotelOps:RoomTypes:v1','Transaction') AS Value").SingleAsync(ct)!="Exclusive")
            throw new Fe05NotificationUnavailableException("FE05_OWNER_CATALOG_LOCK_REQUIRED");
    }
    public async Task RequireAuthorityAsync(HotelOpsDbContext db,Fe05NotificationCommand command,CurrentUserInfo actor,CancellationToken ct)
    {
        var request=owner.Request??throw new Fe05NotificationUnavailableException("FE05_OWNER_OPERATION_REQUIRED");
        await Authorize(db,request,actor,ct);
        if(command.OperationId!=request.OperationId||command.InspectionId!=request.InspectionId||command.BranchId!=request.BranchId
            ||!request.RequiredEvents.Contains(command.Kind)||command.ExpectedRevision!=owner.WrittenRevision)
            throw new Fe05NotificationUnavailableException("FE05_RETAINED_DOMAIN_TRANSITION_REQUIRED");
        await users.ConfirmAfterSourcesAsync(true,ct);
        await ReadPostState(db,command.ExpectedRevision,ct);
    }
    public async Task<bool> CanRecipientReadAsync(HotelOpsDbContext db,Fe05NotificationSource source,long recipient,CancellationToken ct)
    {
        await users.RequireAlreadyLockedAsync(db,source,recipient,ct);
        var candidate=new InspectionRecipientSourceReadCandidate(db,users,TimeProvider.System);
        var roster=users.Roster;
        var roles=source.ReworkId!=null ? new[]{InspectionRecipientRelationship.ReworkAssignee}
            : new[]{InspectionRecipientRelationship.ResponsibleSupervisor,InspectionRecipientRelationship.OriginalCleaner}
                .Where(role=>recipient==(role==InspectionRecipientRelationship.ResponsibleSupervisor?roster.SupervisorId:roster.OriginalCleanerId)).ToArray();
        if(roles.Length==0)throw new Fe05NotificationUnavailableException("FE05_RECIPIENT_RELATIONSHIP_REQUIRED");
        foreach(var role in roles)
            if(!(await candidate.AssessAsync(source,recipient,role,ct)).ExistingGrantAndRelationshipMatch)
                throw new Fe05NotificationUnavailableException("FE05_RECIPIENT_CANDIDATE_DENIED");
        // PL FAST008/009: acceptedConcreteBinding=null for all three recipient types.
        // In particular, a new uncommitted Rework cannot borrow FE04's committed,
        // different-transaction consumer proof. No role/branch/permission inference.
        throw new Fe05NotificationUnavailableException(source.ReworkId is null
            ? "FE05_INSPECTION_RECIPIENT_READ_CONTRACT_REQUIRED" : "FE04_STAGING_WORKER_READ_CONTRACT_REQUIRED");
    }
    public async Task RequireCompletedOperationAsync(HotelOpsDbContext db,Fe05OperationRequest request,CurrentUserInfo actor,
        Fe05OperationReceipt receipt,CancellationToken ct)
    {
        await Authorize(db,request,actor,ct);await users.ConfirmAfterSourcesAsync(true,ct);
        Fe05OperationIdentity.RequireReplay(receipt,actor.UserId,request);
        var json=await db.Database.SqlQuery<string>($"SELECT result_json AS Value FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK) WHERE actor_user_id={actor.UserId} AND operation_key={request.OperationId} AND completed=1 AND fingerprint={Convert.FromHexString(request.RequestFingerprint)}").SingleOrDefaultAsync(ct)
            ??throw new NotificationConflictException();
        using var document=JsonDocument.Parse(json);var root=document.RootElement;
        var roster=users.Roster;
        InspectionOwnerReceipt.Require(root,request,actor.UserId,roster);
        var tag=root.GetProperty("inspectionETag").GetString()!;
        var source=await ReadPostState(db,tag,ct);
        if(root.GetProperty("versionId").GetInt64()!=source.VersionId)throw new NotificationConflictException();
        var writer=root.GetProperty("writerTransactionId").GetInt64();
        var currentTx=await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS Value").SingleAsync(ct);
        if((owner.WrittenRevision!=null)!=(writer==currentTx))throw new NotificationConflictException();
        var issuer=root.GetProperty("issuer").GetString()!;
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.fe04_rework_authority_issuers x WITH(HOLDLOCK)
            JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
            WHERE x.issuer={issuer} AND x.version=1 AND x.is_active=1 AND m.execute_as_principal_id=-2
              AND x.writer_object_id=OBJECT_ID('dbo.fe05_inspection_issuer_v1')
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
            """).SingleAsync(ct)!=1)throw new Fe05NotificationUnavailableException("FE05_PINNED_ISSUER_REQUIRED");
        var rework=request.RequiredEvents.Single()==Fe05NotificationKind.CleaningReworkRequested;
        var entity=rework?"cleaning_reworks":"room_inspections";
        var id=rework?root.GetProperty("reworkId").GetInt64():request.InspectionId;
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.audit_logs WITH(HOLDLOCK)
            WHERE branch_id={request.BranchId} AND actor_user_id={actor.UserId} AND entity_type={entity}
              AND TRY_CONVERT(bigint,entity_id)={id} AND ISJSON(new_values)=1
              AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(new_values,'$.operationId'))={request.OperationId}
            """).SingleAsync(ct)!=1)throw new NotificationConflictException();
        if(rework)
        {
            if(owner.WrittenRevision!=null&&owner.ReworkId!=id)throw new NotificationConflictException();
            var selected=root.GetProperty("selectedDefectIds").EnumerateArray().Select(x=>x.GetInt64()).ToArray();
            var persisted=await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WITH(HOLDLOCK) WHERE rework_id={id} ORDER BY defect_id").ToArrayAsync(ct);
            if(selected.Length is <1 or >200||selected.Any(x=>x<=0)||selected.Distinct().Count()!=selected.Length||!selected.Order().SequenceEqual(persisted))
                throw new NotificationConflictException();
            var sourceDigest=root.GetProperty("sourceDigest").GetString()!;var evidence=root.GetProperty("evidenceDigest").GetString()!;
            if(await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.audit_logs WITH(HOLDLOCK) WHERE branch_id={request.BranchId}
                  AND actor_user_id={actor.UserId} AND entity_type='cleaning_reworks' AND TRY_CONVERT(bigint,entity_id)={id}
                  AND ISJSON(new_values)=1 AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(new_values,'$.operationId'))={request.OperationId}
                  AND JSON_VALUE(new_values,'$.authorityContract')='FE05_REWORK_AUTHORITY_V1' AND JSON_VALUE(new_values,'$.sourceDigest')={sourceDigest}
                """).SingleAsync(ct)!=1)throw new NotificationConflictException();
            if(await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS Value FROM dbo.fs016_rework_source_proofs p WITH(HOLDLOCK)
                JOIN dbo.cleaning_reworks w WITH(HOLDLOCK) ON w.rework_id=p.rework_id
                WHERE p.rework_id={id} AND p.inspection_id={request.InspectionId} AND p.branch_id={request.BranchId}
                  AND p.cleaning_task_id={roster.TaskId} AND p.room_id={roster.RoomId} AND p.version_id={source.VersionId}
                  AND p.origin='FE05_COMMITTED_V1' AND p.contract_version=1 AND p.issuer='FE05.UC045.UC046' AND p.issuer_version=1
                  AND p.actor_user_id={actor.UserId} AND p.operation_key={request.OperationId} AND p.writer_transaction_id={writer}
                  AND p.inspection_revision={Convert.FromHexString(tag.Trim('"'))}
                  AND p.source_digest={Convert.FromHexString(sourceDigest)} AND p.evidence_digest={Convert.FromHexString(evidence)}
                  AND w.inspection_id=p.inspection_id AND w.cleaning_task_id=p.cleaning_task_id AND w.assignee_user_id={roster.AssigneeId}
                """).SingleAsync(ct)!=1)throw new NotificationConflictException();
            if(owner.VerifySnapshot==null)throw new Fe05NotificationUnavailableException("FE04_SNAPSHOT_REVALIDATION_REQUIRED");
            await owner.VerifySnapshot(id,sourceDigest,evidence,ct);
        }
        // Replay/completed receipts must not bypass current recipient authority. Existing
        // persisted graph/proof is necessary, not a replacement for source-read acceptance.
        foreach(var publication in receipt.Publications)
            foreach(var recipient in publication.RecipientIds)users.RequireMember(recipient);
        if(rework)InspectionRecipientPermissionContract.For(InspectionRecipientRelationship.ReworkAssignee).RequireBusinessAcceptance();
        else
        {
            InspectionRecipientPermissionContract.For(InspectionRecipientRelationship.ResponsibleSupervisor).RequireBusinessAcceptance();
            InspectionRecipientPermissionContract.For(InspectionRecipientRelationship.OriginalCleaner).RequireBusinessAcceptance();
        }
        // FE04 owns the projection; attest its persisted audit, not a parallel status engine.
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.audit_logs a WITH(HOLDLOCK) JOIN dbo.rooms r WITH(HOLDLOCK)
              ON TRY_CONVERT(bigint,a.entity_id)=r.room_id
            WHERE a.entity_type='rooms' AND r.room_id={roster.RoomId} AND r.branch_id={request.BranchId}
              AND a.actor_user_id={actor.UserId} AND ISJSON(a.new_values)=1
              AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(a.new_values,'$.projectionOperation'))={request.OperationId}
              AND JSON_VALUE(a.new_values,'$.readiness')='REWORK_REQUIRED'
              AND JSON_VALUE(a.new_values,'$.operationalStatus')=r.operational_status
              AND JSON_VALUE(a.new_values,'$.effectiveBlock')=CASE WHEN EXISTS(SELECT 1 FROM dbo.room_blocks b WITH(HOLDLOCK)
                WHERE b.room_id=r.room_id AND b.blocked_at<=SYSUTCDATETIME() AND (b.released_at IS NULL OR b.released_at>SYSUTCDATETIME()))
                THEN 'true' ELSE 'false' END
            """).SingleAsync(ct)!=1)throw new NotificationConflictException();
    }
    private async Task Authorize(HotelOpsDbContext db,Fe05OperationRequest request,CurrentUserInfo actor,CancellationToken ct)
    {
        if(!ReferenceEquals(db,context)||!current.TryGetCurrentUser(out var live)||live!=actor)throw new Fe05NotificationUnavailableException("FE05_OWNER_ACTOR_INVALID");
        owner.Require(db,request,actor);users.RequireMember(actor.UserId);
        var valid=await sessions.ValidateSessionAsync(owner.Ticket!,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=actor.SessionId||valid.Session.IsRestricted
            ||!(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",request.BranchId,ct)).IsGranted)
            throw new Fe05NotificationUnavailableException("FE05_OWNER_AUTHORIZATION_INVALID");
    }
    private async Task<PostState> ReadPostState(HotelOpsDbContext db,string tag,CancellationToken ct)
    {
        var r=users.Roster;
        return await db.Database.SqlQuery<PostState>($"""
            SELECT i.version_id AS VersionId FROM dbo.room_inspections i WITH(HOLDLOCK)
            JOIN dbo.cleaning_tasks t WITH(HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms room WITH(HOLDLOCK) ON room.room_id=i.room_id AND room.branch_id={r.BranchId}
            JOIN dbo.room_cleaning_cycle_state c WITH(HOLDLOCK) ON c.room_id=room.room_id AND c.cleaning_task_id=t.cleaning_task_id
            WHERE i.inspection_id={r.InspectionId} AND t.cleaning_task_id={r.TaskId} AND i.room_id={r.RoomId}
              AND i.inspector_user_id={r.ActorId} AND i.row_version={Convert.FromHexString(tag.Trim('"'))}
              AND i.status='COMPLETED' AND i.result='FAIL' AND i.workflow_status='FAILED' AND i.inspected_at IS NOT NULL
              AND t.status='IN_PROGRESS' AND t.workflow_status='REWORK_REQUIRED'
              AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections n WITH(HOLDLOCK) WHERE n.cleaning_task_id=t.cleaning_task_id AND n.inspection_id>i.inspection_id)
            """).SingleOrDefaultAsync(ct)??throw new NotificationConflictException();
    }
    private sealed class PostState {public long VersionId{get;set;}}
}
internal static class InspectionOwnerReceipt
{
    internal static void Require(JsonElement root,Fe05OperationRequest request,long actor,InspectionRecipientRoster roster)
    {
        bool rework=request.RequiredEvents.Single()==Fe05NotificationKind.CleaningReworkRequested;
        if(root.GetProperty("contract").GetString()!=(rework?"FE05_REWORK_AUTHORITY_V1":"FE05_INSPECTION_CONCLUSION_V1")
            ||root.GetProperty("issuer").GetString()!=(rework?"FE05.UC045.UC046":"FE05.UC044.UC045")
            ||root.GetProperty("issuerVersion").GetInt32()!=1||root.GetProperty("actorUserId").GetInt64()!=actor
            ||root.GetProperty("operationId").GetGuid()!=request.OperationId||root.GetProperty("requestFingerprint").GetString()!=request.RequestFingerprint
            ||root.GetProperty("branchId").GetInt64()!=request.BranchId||root.GetProperty("inspectionId").GetInt64()!=request.InspectionId
            ||root.GetProperty("cleaningTaskId").GetInt64()!=roster.TaskId||root.GetProperty("roomId").GetInt64()!=roster.RoomId
            ||root.GetProperty("writerTransactionId").GetInt64()<=0||root.GetProperty("conclusion").GetString()!="FAILED")
            throw new NotificationConflictException();
        if(rework&&(root.GetProperty("origin").GetString()!="FE05_COMMITTED_V1"||root.GetProperty("version").GetInt32()!=1||root.GetProperty("reworkId").GetInt64()<=0))
            throw new NotificationConflictException();
    }
}
