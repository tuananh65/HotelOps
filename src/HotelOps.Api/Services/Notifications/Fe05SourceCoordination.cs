using System.Data;
using System.Data.Common;
using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// Backend owner boundary, never an HTTP model or a configurable approval flag.
// DEV1 must attest the retained user/session/branch/catalog/cycle/source lock protocol,
// and its FAILED conclusion authority, including rework origin when applicable.
public interface IFe05SourceCoordination
{
    Task RequireReservationAsync(HotelOpsDbContext db, Fe05OperationRequest request,
        CurrentUserInfo actor, CancellationToken ct);
    Task RequireCompletedOperationAsync(HotelOpsDbContext db, Fe05OperationRequest request,
        CurrentUserInfo actor, Fe05OperationReceipt receipt, CancellationToken ct);
    Task RequireAuthorityAsync(HotelOpsDbContext db, Fe05NotificationCommand command,
        CurrentUserInfo actor, CancellationToken ct);
    Task<bool> CanRecipientReadAsync(HotelOpsDbContext db, Fe05NotificationSource source,
        long recipientId, CancellationToken ct);
}

public sealed class UnacceptedFe05Coordination : IFe05SourceCoordination
{
    public Task RequireCompletedOperationAsync(HotelOpsDbContext db, Fe05OperationRequest request,
        CurrentUserInfo actor, Fe05OperationReceipt receipt, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new Fe05NotificationUnavailableException("FE05_DOMAIN_RECEIPT_AND_AUTHORITY_NOT_ACCEPTED"); }
    public Task RequireReservationAsync(HotelOpsDbContext db, Fe05OperationRequest request,
        CurrentUserInfo actor, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new Fe05NotificationUnavailableException("FE05_OWNER_PREMUTATION_PROTOCOL_NOT_ACCEPTED"); }
    public Task RequireAuthorityAsync(HotelOpsDbContext db, Fe05NotificationCommand command,
        CurrentUserInfo actor, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new Fe05NotificationUnavailableException("FE05_OWNER_LOCK_AND_AUTHORITY_PROTOCOL_NOT_ACCEPTED"); }
    public Task<bool> CanRecipientReadAsync(HotelOpsDbContext db, Fe05NotificationSource source,
        long recipientId, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); throw new Fe05NotificationUnavailableException("FE05_SOURCE_READ_POLICY_NOT_ACCEPTED"); }
}

public static class Fe05CallerTransaction
{
    public static Guid Require(HotelOpsDbContext db, DbConnection? connection = null, DbTransaction? transaction = null)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not { } tx ||
            tx.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable ||
            System.Transactions.Transaction.Current is not null ||
            connection is not null && !ReferenceEquals(connection, db.Database.GetDbConnection()) ||
            transaction is not null && (!ReferenceEquals(transaction, tx.GetDbTransaction()) ||
                !ReferenceEquals(transaction.Connection, db.Database.GetDbConnection())))
            throw new InvalidOperationException("FE05_EXACT_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED");
        return tx.TransactionId;
    }
}

// Reads exact a5048a7 post-mutation columns without changing generated EF entities.
// Authority is deliberately not inferred from a persisted FAIL alone (857fe0d contract).
public sealed class Fe05SqlNotificationSource(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, IFe05SourceCoordination coordination) : IFe05NotificationSource
{
    public async Task<Fe05NotificationSource> LoadLockedAsync(Fe05NotificationCommand command, long actorId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Fe05CallerTransaction.Require(db);
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != actorId || actor.UserId <= 0 ||
            actor.SessionId <= 0 || actor.IsRestricted || command.BranchId <= 0 || command.InspectionId <= 0 ||
            command.OperationId == Guid.Empty || !Enum.IsDefined(command.Kind))
            throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
        await AuthorizeAsync(actor, command, ct);
        var requiresInspector=command.Kind==Fe05NotificationKind.InspectionFailed;
        var row = await db.Database.SqlQuery<SourceRow>($"""
            SELECT i.inspection_id AS InspectionId,i.room_id AS RoomId,r.branch_id AS BranchId,
              i.cleaning_task_id AS CleaningTaskId,i.version_id AS VersionId,i.status AS Status,
              i.result AS Result,i.workflow_status AS WorkflowState,i.row_version AS Revision,
              i.inspected_at AS InspectedAt,c.cleaning_task_id AS CurrentTaskId
            FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.cleaning_tasks t WITH(UPDLOCK,HOLDLOCK) ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.rooms r WITH(HOLDLOCK) ON r.room_id=i.room_id
            JOIN dbo.floors f WITH(HOLDLOCK) ON f.floor_id=r.floor_id AND f.branch_id=r.branch_id
            JOIN dbo.branches b WITH(HOLDLOCK) ON b.branch_id=r.branch_id
            JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=i.room_id
            WHERE i.inspection_id={command.InspectionId} AND r.branch_id={command.BranchId}
              AND b.is_active=1 AND r.is_active=1 AND f.is_active=1
              AND (r.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas a WITH(HOLDLOCK)
                WHERE a.area_id=r.area_id AND a.branch_id=r.branch_id AND a.is_active=1
                  AND (a.floor_id IS NULL OR a.floor_id=r.floor_id)))
              AND ({requiresInspector}=0 OR i.inspector_user_id={actorId})
              AND t.workflow_status='REWORK_REQUIRED' AND t.status='IN_PROGRESS'
              AND NOT EXISTS(SELECT 1 FROM dbo.room_inspections newer WITH(HOLDLOCK)
                WHERE newer.cleaning_task_id=i.cleaning_task_id AND newer.inspection_id>i.inspection_id)
            """).SingleOrDefaultAsync(ct)
            ?? throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
        if (row.Revision.Length != 8 || row.InspectedAt is null)
            throw new Fe05NotificationUnavailableException("FE05_AUTHORITATIVE_FAILED_STATE_REQUIRED");
        long? reworkId = null;
        string? reworkRevision=null;
        var occurred = DateTime.SpecifyKind(row.InspectedAt.Value, DateTimeKind.Utc);
        if (command.Kind == Fe05NotificationKind.CleaningReworkRequested)
        {
            var rework = await db.Database.SqlQuery<ReworkRow>($"""
                SELECT w.rework_id AS ReworkId,w.requested_at AS RequestedAt,w.row_version AS Revision
                FROM dbo.cleaning_reworks w WITH(UPDLOCK,HOLDLOCK)
                WHERE w.inspection_id={row.InspectionId} AND w.cleaning_task_id={row.CleaningTaskId}
                  AND w.status='PENDING' AND w.workflow_status='PENDING_ACCEPTANCE'
                  AND w.requested_at>={row.InspectedAt} AND w.assignee_user_id IS NOT NULL AND w.due_at IS NOT NULL
                  AND EXISTS(SELECT 1 FROM dbo.cleaning_rework_defects l WITH(HOLDLOCK) WHERE l.rework_id=w.rework_id)
                  AND NOT EXISTS(SELECT 1 FROM dbo.cleaning_rework_defects l WITH(HOLDLOCK)
                    JOIN dbo.inspection_defects d WITH(HOLDLOCK) ON d.defect_id=l.defect_id
                    WHERE l.rework_id=w.rework_id AND (d.inspection_id<>w.inspection_id OR NOT EXISTS(
                      SELECT 1 FROM dbo.inspection_results ir WITH(HOLDLOCK)
                      JOIN dbo.inspection_checklist_items ci WITH(HOLDLOCK) ON ci.item_id=ir.checklist_item_id
                      WHERE ir.inspection_id=w.inspection_id AND ir.checklist_item_id=d.checklist_item_id
                        AND ci.version_id={row.VersionId} AND ir.outcome='FAIL')))
                """).ToArrayAsync(ct);
            if (rework.Length != 1) throw new Fe05NotificationUnavailableException("FE05_EXACT_REWORK_BINDING_REQUIRED");
            reworkId = rework[0].ReworkId;
            if(rework[0].Revision.Length!=8)throw new Fe05NotificationUnavailableException("FE05_REWORK_REVISION_REQUIRED");
            reworkRevision='"'+Convert.ToHexString(rework[0].Revision)+'"';
            occurred = DateTime.SpecifyKind(rework[0].RequestedAt, DateTimeKind.Utc);
        }
        var results = reworkId is not null ? await db.Database.SqlQuery<long>($"""
            SELECT DISTINCT TOP(201) ir.result_id AS Value FROM dbo.cleaning_rework_defects l WITH(HOLDLOCK)
            JOIN dbo.inspection_defects d WITH(HOLDLOCK) ON d.defect_id=l.defect_id AND d.inspection_id={row.InspectionId}
            JOIN dbo.inspection_results ir WITH(HOLDLOCK) ON ir.inspection_id=d.inspection_id AND ir.checklist_item_id=d.checklist_item_id
            JOIN dbo.inspection_checklist_items ci WITH(HOLDLOCK) ON ci.item_id=ir.checklist_item_id AND ci.version_id={row.VersionId}
            WHERE l.rework_id={reworkId} AND ir.outcome='FAIL' ORDER BY ir.result_id
            """).ToArrayAsync(ct) : await db.InspectionResults.AsNoTracking().Where(x => x.InspectionId == row.InspectionId &&
            x.Outcome == "FAIL" && x.ChecklistItem.VersionId == row.VersionId).OrderBy(x => x.ResultId)
            .Select(x => x.ResultId).Take(201).ToArrayAsync(ct);
        await AuthorizeAsync(actor, command, ct); // includes owner/session revalidation after waits
        var snapshot = new Fe05NotificationSource(actorId,row.BranchId,row.RoomId,row.InspectionId,row.CleaningTaskId,
            row.VersionId,reworkId,'"'+Convert.ToHexString(row.Revision)+'"',row.CurrentTaskId,row.Status,row.Result ?? "",
            row.WorkflowState, new DateTimeOffset(occurred),results,reworkRevision);
        // Reuse the production mapper's complete state validation before returning source.
        Fe05NotificationMapping.ValidateSource(command,snapshot,actorId);
        return snapshot;
    }
    private async Task AuthorizeAsync(CurrentUserInfo actor, Fe05NotificationCommand command, CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var observed) || observed!=actor || observed.IsRestricted)
            throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
        await coordination.RequireAuthorityAsync(db,command,actor,ct);
        if (!(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",command.BranchId,ct)).IsGranted)
            throw new Fe05NotificationUnavailableException("FE05_SOURCE_UNAVAILABLE");
    }
    private sealed class SourceRow
    {
        public long InspectionId {get;set;} public long RoomId {get;set;} public long BranchId {get;set;}
        public long CleaningTaskId {get;set;} public long VersionId {get;set;} public long CurrentTaskId {get;set;}
        public string Status {get;set;}=""; public string? Result {get;set;} public string WorkflowState {get;set;}="";
        public byte[] Revision {get;set;}=[]; public DateTime? InspectedAt {get;set;}
    }
    private sealed class ReworkRow { public long ReworkId {get;set;} public DateTime RequestedAt {get;set;} public byte[] Revision {get;set;}=[]; }
}
