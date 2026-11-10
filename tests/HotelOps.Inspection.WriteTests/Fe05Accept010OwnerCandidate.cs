#if ACCEPT010_REFERENCE
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Accept010;
using HotelOps.Sql09;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

// FE05-owned candidate. Compiles against unmodified original package interfaces/types,
// not hand-written copies. ACCEPT010 hold is checked before resolving any fixture/context.
internal sealed class Fe05Accept010OwnerDriver(AcceptanceGate gate,SignedGrant signed,
    Func<string,ExecutionGrant,CancellationToken,Task<Fe05AcceptedCaseScope>>? bind) : IAcceptedFe05OwnerCaseDriver
{
    public async Task<OwnerCaseEvidence> ExecuteAsync(string originalCaseId,ExecutionGrant grant,CancellationToken ct)
    {
        var accepted=await gate.RequireAsync(AcceptanceGate.Checkpoint,signed,"L2:"+originalCaseId,ct);
        if(accepted!=grant)throw new InvalidOperationException("FE05_ACCEPT010_GRANT_DRIFT");
        foreach(var role in Enum.GetValues<InspectionRecipientRelationship>())InspectionRecipientPermissionContract.For(role).RequireBusinessAcceptance();
        var scope=await (bind??throw new InvalidOperationException("FE05_ACCEPTED_CASE_FIXTURES_MISSING"))(originalCaseId,accepted,ct);
        return await scope.ExecuteAsync(originalCaseId,ct);
    }
}

// Scope belongs to a future independently accepted fixture host. No default factory,
// runtime profile, loose boolean activation or direct SQL domain mutation is supplied.
internal abstract class Fe05AcceptedCaseScope
{
    internal abstract Task<OwnerCaseEvidence> ExecuteAsync(string originalCaseId,CancellationToken ct);
}

internal sealed class Fe05OwnerDomainCalls(HotelOpsDbContext db,RoomTypeCreationService service,
    SessionReference ticket,long sid,Fe05OperationStaging staging,NotificationCommitGuard guard,InspectionUsersPrelock users)
{
    internal async Task<string> RejectAsync(long branch,long inspection,string revision,Guid operation,RejectInspectionRequest input,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);guard.RequireAttached(db,db.Database.CurrentTransaction!.TransactionId);
        users.RequireMember(ticket.UserId);await users.ConfirmAfterSourcesAsync(false,ct);
        return await service.ComposeRejectInspectionAsync(ticket,sid,branch,inspection,revision,operation,input,staging,guard,ct);
    }
    internal async Task<string> ReworkAsync(long branch,long inspection,string revision,Guid operation,RequestReworkRequest input,string evidenceRoot,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);guard.RequireAttached(db,db.Database.CurrentTransaction!.TransactionId);
        users.RequireMember(ticket.UserId);users.RequireMember(input.AssigneeId);await users.ConfirmAfterSourcesAsync(false,ct);
        return await service.ComposeInspectionReworkAsync(ticket,sid,branch,inspection,revision,operation,input,evidenceRoot,staging,guard,ct);
    }
    internal Task<InspectionRecoveredOutcome> ObserveUnknownAsync(Fe05OperationRequest request,IFe05OperationStore store,IFe05SourceCoordination coordination,string evidenceRoot,CancellationToken ct)
        =>service.ObserveInspectionOutcomeInCallerTransactionAsync(ticket,sid,request,store,coordination,guard,ct,evidenceRoot);
    // Scope explicitly owns Begin/guarded Commit/Rollback. Unknown outcome method above
    // observes original identity only: it never calls Reject/Rework or creates a new key.
}

internal sealed class Fe05Accept010CaseObserver(AcceptanceGate gate,SignedGrant signed,
    Func<AcceptanceCase,ExecutionGrant,CancellationToken,Task<Fe05IndependentObservationScope>>? bind) : IAcceptedFe05CaseObserver
{
    private readonly Dictionary<string,Fe05IndependentObservationScope> observations=[];
    public async Task RequireBindingsBeforeAsync(AcceptanceCase plan,ExecutionGrant grant,CancellationToken ct)
    {
        await gate.RequireAsync(AcceptanceGate.Checkpoint,signed,"L2:"+plan.Id,ct);
        var scope=await (bind??throw new InvalidOperationException("FE05_ACCEPTED_INDEPENDENT_OBSERVER_MISSING"))(plan,grant,ct);
        await scope.RequireBeforeAsync(plan,grant,ct);
        if(!observations.TryAdd(plan.Id,scope))throw new InvalidOperationException("FE05_OBSERVER_CASE_ALREADY_BOUND");
    }
    public async Task RequireActualCaseAsync(AcceptanceCase plan,ExecutionGrant grant,OwnerCaseEvidence actual,CancellationToken ct)
    {
        await gate.RequireAsync(AcceptanceGate.Checkpoint,signed,"L2:"+plan.Id,ct);
        if(!observations.TryGetValue(plan.Id,out var scope))throw new InvalidOperationException("FE05_INDEPENDENT_BEFORE_SNAPSHOT_REQUIRED");
        OwnerEvidence.RequireMinimumEvidence(plan.Id,actual,plan.Orderings);
        await scope.RequireActualAsync(plan,grant,actual,ct);
    }
}
internal abstract class Fe05IndependentObservationScope
{
    internal abstract Task RequireBeforeAsync(AcceptanceCase plan,ExecutionGrant grant,CancellationToken ct);
    internal abstract Task RequireActualAsync(AcceptanceCase plan,ExecutionGrant grant,OwnerCaseEvidence actual,CancellationToken ct);
}

// Independent actual DB reads, not expected-to-actual copies. The accepted host must
// authenticate fixture inventory/target/assembly and protected baseline before calling.
internal static class Fe05ActualCaseSqlObservation
{
    internal sealed record Snapshot(string RootState,string TaskState,long? ReworkId,string RootTag,int Reservations,int Publications,int Intents,int Orphans,string DomainReceipt,bool ModulePinned,bool TrustedProof);
    internal static async Task<Snapshot> ReadAsync(HotelOpsDbContext db,long actor,long branch,long inspection,Guid operation,CancellationToken ct)
    {
        Fe05CallerTransaction.Require(db);
        var rows=await db.Database.SqlQuery<Row>($"""
          SELECT i.workflow_status AS RootState,t.workflow_status AS TaskState,
           (SELECT TRY_CONVERT(bigint,JSON_VALUE(o.result_json,'$.reworkId')) FROM dbo.notification_operation_reservations o WHERE o.actor_user_id={actor} AND o.operation_key={operation}) AS ReworkId,
           CHAR(34)+CONVERT(varchar(16),i.row_version,2)+CHAR(34) AS RootTag,
           (SELECT COUNT(*) FROM dbo.notification_operation_reservations o WHERE o.actor_user_id={actor} AND o.operation_key={operation}) AS Reservations,
           (SELECT COUNT(*) FROM dbo.notification_publications p WHERE p.actor_user_id={actor} AND p.operation_key={operation}) AS Publications,
           (SELECT COUNT(*) FROM dbo.notification_delivery_intents d JOIN dbo.notification_publications p ON p.publication_id=d.publication_id WHERE p.actor_user_id={actor} AND p.operation_key={operation}) AS Intents,
           (SELECT COUNT(*) FROM dbo.notification_delivery_intents d LEFT JOIN dbo.notification_publications p ON p.publication_id=d.publication_id WHERE p.publication_id IS NULL) AS Orphans,
           ISNULL((SELECT o.result_json FROM dbo.notification_operation_reservations o WHERE o.actor_user_id={actor} AND o.operation_key={operation} AND o.completed=1),N'') AS DomainReceipt,
           CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
             WHERE x.is_active=1 AND x.version=1 AND x.issuer='FE05.UC044.UC045' AND m.execute_as_principal_id=-2 AND x.module_digest=HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))) THEN 1 ELSE 0 END) AS ModulePinned,
           CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM dbo.fs016_rework_source_proofs p WHERE p.inspection_id=i.inspection_id AND p.origin='FE05_COMMITTED_V1' AND p.operation_key={operation} AND p.actor_user_id={actor} AND p.inspection_revision=i.row_version AND p.cleaning_task_id=t.cleaning_task_id AND p.room_id=i.room_id AND p.branch_id={branch}) THEN 1 ELSE 0 END) AS TrustedProof
          FROM dbo.room_inspections i JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
          JOIN dbo.rooms r ON r.room_id=i.room_id AND r.branch_id={branch}
          JOIN dbo.room_cleaning_cycle_state c ON c.room_id=i.room_id AND c.cleaning_task_id=i.cleaning_task_id
          WHERE i.inspection_id={inspection} AND i.inspector_user_id={actor}
          """).ToArrayAsync(ct);
        if(rows.Length!=1)throw new InvalidOperationException("FE05_OBSERVER_CURRENT_EXACT_SOURCE_REQUIRED");
        var x=rows[0];return new(x.RootState,x.TaskState,x.ReworkId,x.RootTag,x.Reservations,x.Publications,x.Intents,x.Orphans,x.DomainReceipt,x.ModulePinned,x.TrustedProof);
    }
    internal static void RequireCommittedSource(Snapshot actual,Fe05OperationRequest request,long actor)
    {
        if(actual.RootState!="FAILED"||actual.TaskState!="REWORK_REQUIRED"||actual.Orphans!=0||actual.Reservations!=1||actual.Publications!=request.RequiredEvents.Count||actual.Intents<=0||!actual.ModulePinned)
            throw new InvalidOperationException("FE05_OBSERVER_PERSISTED_DOMAIN_GRAPH_REQUIRED");
        using var json=JsonDocument.Parse(actual.DomainReceipt);var r=json.RootElement;
        var rework=request.RequiredEvents.Contains(Fe05NotificationKind.CleaningReworkRequested);
        if(r.GetProperty("contract").GetString()!=(rework?"FE05_REWORK_AUTHORITY_V1":"FE05_INSPECTION_CONCLUSION_V1")
            ||r.GetProperty("issuer").GetString()!=(rework?"FE05.UC045.UC046":"FE05.UC044.UC045")||r.GetProperty("issuerVersion").GetInt32()!=1
            ||r.GetProperty("conclusion").GetString()!="FAILED"||r.GetProperty("writerTransactionId").GetInt64()<=0
            ||rework&&(r.GetProperty("origin").GetString()!="FE05_COMMITTED_V1"||r.GetProperty("reworkId").GetInt64()!=actual.ReworkId))
            throw new InvalidOperationException("FE05_OBSERVER_ACTUAL_RECEIPT_AUTHORITY_MISMATCH");
        if(r.GetProperty("actorUserId").GetInt64()!=actor||r.GetProperty("branchId").GetInt64()!=request.BranchId||r.GetProperty("inspectionId").GetInt64()!=request.InspectionId||r.GetProperty("operationId").GetGuid()!=request.OperationId
            ||r.GetProperty("requestFingerprint").GetString()!=request.RequestFingerprint||r.GetProperty("inspectionETag").GetString()!=actual.RootTag)
            throw new InvalidOperationException("FE05_OBSERVER_ACTUAL_RECEIPT_IDENTITY_MISMATCH");
        if(request.RequiredEvents.Contains(Fe05NotificationKind.CleaningReworkRequested)&&(!actual.TrustedProof||actual.ReworkId is not >0))throw new InvalidOperationException("FE05_OBSERVER_REAL_REWORK_PROOF_REQUIRED");
        // Not sufficient alone: accepted observation scope still verifies recipient policy,
        // actual prelock retention, exact graph payloads, original FE04 snapshot/digests,
        // projection, protected inventory, both concurrency orders and cleanup independently.
    }
    private sealed class Row {public string RootState{get;set;}="";public string TaskState{get;set;}="";public long? ReworkId{get;set;}public string RootTag{get;set;}="";public int Reservations{get;set;}public int Publications{get;set;}public int Intents{get;set;}public int Orphans{get;set;}public string DomainReceipt{get;set;}="";public bool ModulePinned{get;set;}public bool TrustedProof{get;set;}}
}
#endif
