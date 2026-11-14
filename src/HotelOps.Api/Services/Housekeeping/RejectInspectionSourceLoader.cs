using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // Internal preparation only. No endpoint/DI activation; no Begin/Commit/Rollback or business writes.
    internal Task<RejectInspectionLoadResult> LoadRejectInspectionSourceAsync(SessionReference ticket, long sid,
        RejectInspectionLoadCommand command, CancellationToken ct)
        => RejectInspectionLoading.LoadAsync(new RejectReadSession(this, db, ticket, sid), command, ct);

    private async Task<RoomTypeBatchStatus?> AuthorizeRejectLoadAsync(SessionReference ticket, long sid, long branch, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId
            || actor.SessionId != sid || actor.IsRestricted || ticket.IsRestricted)
            return RoomTypeBatchStatus.SessionInvalid;
        await new UserEligibilityLock(db).LockAsync([actor.UserId], ct);
        var valid = await sessions.ValidateSessionAsync(ticket, ct);
        if (!valid.IsValid || valid.Session?.SessionId != sid || valid.Session.IsRestricted)
            return RoomTypeBatchStatus.SessionInvalid;
        var permission = await permissions.EvaluateBranchAsync("INSPECTION_PERFORM", branch, ct);
        return permission.IsGranted ? null : permission.Decision == Security.Authorization.PermissionDecision.Unavailable
            ? RoomTypeBatchStatus.Unavailable : RoomTypeBatchStatus.Forbidden;
    }

    private sealed class RejectReadSession(RoomTypeCreationService owner, HotelOpsDbContext db, SessionReference ticket, long sid)
        : IRejectInspectionReadSession
    {
        public void RequireCallerTransaction()
        {
            if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not { } tx
                || tx.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
                || db.ChangeTracker.Entries().Any() || System.Transactions.Transaction.Current != null)
                throw new InvalidOperationException("Reject source loading requires the caller's clean SERIALIZABLE SQL transaction.");
        }

        public Task<RoomTypeBatchStatus?> AuthorizeAsync(long branch, CancellationToken ct)
            => owner.AuthorizeRejectLoadAsync(ticket, sid, branch, ct);

        public async Task<RejectInspectionLoadedSource?> ReadLockedAsync(long branch, long inspection, CancellationToken ct)
        {
            // Same user/session -> branch -> catalog ordering as BatchAsync. No Work-Time operation here.
            var location = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (location == null || !location.IsActive) return null;
            var acquired = await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={CatalogLock},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @r AS [Value];").ToArrayAsync(ct);
            if (acquired.Length != 1 || acquired[0] < 0) throw new RejectInspectionSourceUnavailable("INSPECTION_SOURCE_LOCK_UNAVAILABLE");
            if (!await HousekeepingSchema.InstalledAsync(db, ct)) throw new RejectInspectionSourceUnavailable("INSPECTION_SOURCE_SCHEMA_REQUIRED");
            if(!await owner.InspectionEvidenceRevisionInstalled(ct))
                throw new RejectInspectionSourceUnavailable("INSPECTION_EVIDENCE_REVISION_REQUIRED");
            var rooms = await owner.HistoryRooms(ct);
            if (rooms == null) throw new RejectInspectionSourceUnavailable("INSPECTION_ROOM_SCHEMA_REQUIRED");
            var scoped = rooms.Where(r => r.BranchId == branch && r.IsActive && r.Floor.IsActive
                && (r.AreaId == null || r.Area!.IsActive)).Select(r => r.RoomId);
            var row = await db.RoomInspections.FromSqlInterpolated($"SELECT * FROM dbo.room_inspections WITH(UPDLOCK,HOLDLOCK) WHERE inspection_id={inspection}")
                .AsNoTracking().SingleOrDefaultAsync(i => scoped.Contains(i.RoomId), ct);
            if (row == null) return null;
            if (row.InspectorUserId != ticket.UserId) throw new RejectInspectionOwnershipException();
            var task = await db.CleaningTasks.FromSqlInterpolated($"SELECT * FROM dbo.cleaning_tasks WITH(UPDLOCK,HOLDLOCK) WHERE cleaning_task_id={row.CleaningTaskId}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            var version = await db.InspectionChecklistVersions.AsNoTracking().SingleOrDefaultAsync(v => v.VersionId == row.VersionId, ct);
            if (task == null || version == null) return null;
            // Retired versions remain the assigned source. Never select latest/published replacement.
            var criteria = await db.InspectionChecklistItems.AsNoTracking().Where(c => c.VersionId == row.VersionId).ToArrayAsync(ct);
            var results = await db.InspectionResults.AsNoTracking().Where(r => r.InspectionId == inspection).ToArrayAsync(ct);
            var defects = await db.InspectionDefects.AsNoTracking().Where(d => d.InspectionId == inspection).ToArrayAsync(ct);
            var state = await owner.InspectionState(inspection, ct);
            var evidence = await db.Database.SqlQuery<RejectEvidenceRow>($"""
                SELECT a.attachment_id AS AttachmentId,a.entity_id AS EntityId,a.entity_type AS EntityType,a.deleted_at AS DeletedAt
                FROM dbo.inspection_defect_attachments l JOIN dbo.inspection_defects d ON d.defect_id=l.defect_id
                JOIN dbo.attachments a ON a.attachment_id=l.attachment_id WHERE d.inspection_id={inspection}
                UNION
                SELECT a.attachment_id,a.entity_id,a.entity_type,a.deleted_at
                FROM dbo.inspection_result_attachments l JOIN dbo.inspection_results r ON r.result_id=l.result_id
                JOIN dbo.attachments a ON a.attachment_id=l.attachment_id WHERE r.inspection_id={inspection}
                UNION SELECT a.attachment_id,a.entity_id,a.entity_type,a.deleted_at FROM dbo.attachments a WHERE a.entity_type='INSPECTION' AND a.entity_id={inspection}
                """).ToArrayAsync(ct);
            long? currentCycle=null;
            if(await owner.LifecycleInstalled(ct))
                currentCycle=await db.Database.SqlQuery<long>($"SELECT cleaning_task_id AS Value FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK) WHERE room_id={row.RoomId}").SingleOrDefaultAsync(ct);
            return new(new(row, task, version, criteria, results, defects, state.State, state.Revision),
                evidence.Select(e => new RejectInspectionEvidence(e.AttachmentId, e.EntityId, e.EntityType, e.DeletedAt)).ToArray(),currentCycle);
        }
    }

    private sealed class RejectEvidenceRow
    {
        public long AttachmentId { get; set; }
        public long EntityId { get; set; }
        public string EntityType { get; set; } = "";
        public DateTime? DeletedAt { get; set; }
    }
}
