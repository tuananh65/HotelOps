using System.Data;
using System.Runtime.CompilerServices;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

[assembly: InternalsVisibleTo("HotelOps.Inspection.WriteTests")]
namespace HotelOps.Api.Services.Housekeeping;

// Caller-owned SQL primitives, not a committed conclusion receipt or public authorization boundary.
// Production callers must additionally satisfy notification, status projection and durable source gates.
internal static class InspectionConclusionSql
{
    internal static void RequireTransaction(HotelOpsDbContext db)
    {
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not { } tx
            || tx.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("INSPECTION_CALLER_SERIALIZABLE_TRANSACTION_REQUIRED");
    }

    internal static async Task RejectAsync(HotelOpsDbContext db, RejectInspectionLoadedSource source,
        RejectInspectionInput input, CancellationToken ct)
    {
        RequireTransaction(db);
        var validation = RejectInspectionValidator.Validate(source.Domain, input);
        if (!validation.IsValid || source.CurrentCleaningTaskId != source.Domain.CleaningTask.CleaningTaskId)
            throw new InvalidOperationException("INSPECTION_VALIDATED_CURRENT_SOURCE_REQUIRED");
        var i = source.Domain.Inspection; var revision = source.Domain.Revision;
        // Conditions are checked again by SQL under retained locks. Never commit a partial transition.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET XACT_ABORT ON;
            IF NOT EXISTS(SELECT 1 FROM dbo.room_cleaning_cycle_state WITH(UPDLOCK,HOLDLOCK)
                WHERE room_id={i.RoomId} AND cleaning_task_id={i.CleaningTaskId})
                THROW 51990,'INSPECTION_CURRENT_CYCLE_REQUIRED',1;
            UPDATE dbo.room_inspections SET workflow_status='FAILED',status='COMPLETED',result='FAIL',
                inspected_at=SYSUTCDATETIME(),summary_note={input.Reason!.Trim()}
                WHERE inspection_id={i.InspectionId} AND room_id={i.RoomId} AND cleaning_task_id={i.CleaningTaskId}
                  AND version_id={i.VersionId} AND row_version={revision}
                  AND workflow_status='PENDING_CONCLUSION' AND status='IN_PROGRESS' AND result IS NULL;
            IF @@ROWCOUNT<>1 THROW 51990,'INSPECTION_WRITE_CONFLICT',1;
            UPDATE dbo.cleaning_tasks SET workflow_status='REWORK_REQUIRED'
                WHERE cleaning_task_id={i.CleaningTaskId} AND room_id={i.RoomId}
                  AND workflow_status='PENDING_INSPECTION' AND status='IN_PROGRESS';
            IF @@ROWCOUNT<>1 THROW 51990,'CLEANING_PHASE_CONFLICT',1;
            """, ct);
    }

    internal static async Task<long> CreateReworkAsync(HotelOpsDbContext db, long branch, long inspection,
        byte[] expectedRevision, long assignee, DateTime due, string reason, IReadOnlyList<long> defects, CancellationToken ct)
    {
        RequireTransaction(db);
        if (branch<=0 || inspection<=0 || expectedRevision.Length!=8 || assignee<=0 || due.Kind!=DateTimeKind.Utc
            || due<=DateTime.UtcNow || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length>500 || reason.Any(char.IsControl)
            || defects.Count is <1 or >200 || defects.Any(x=>x<=0) || defects.Distinct().Count()!=defects.Count)
            throw new InvalidOperationException("REWORK_INPUT_INVALID");
        // Persisted FAIL alone is deliberately insufficient for the public UC046 path. Its authority gate remains closed.
        var sources=await db.Database.SqlQuery<FailedRow>($"""
            SELECT i.cleaning_task_id AS TaskId,i.room_id AS RoomId FROM dbo.room_inspections i WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.rooms r ON r.room_id=i.room_id JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=i.cleaning_task_id AND t.room_id=i.room_id
            JOIN dbo.room_cleaning_cycle_state c WITH(UPDLOCK,HOLDLOCK) ON c.room_id=i.room_id AND c.cleaning_task_id=t.cleaning_task_id
            WHERE i.inspection_id={inspection} AND r.branch_id={branch} AND i.row_version={expectedRevision}
              AND i.workflow_status='FAILED' AND i.status='COMPLETED' AND i.result='FAIL' AND i.inspected_at IS NOT NULL
              AND t.workflow_status='REWORK_REQUIRED'
            """).ToArrayAsync(ct);
        if(sources.Length!=1 || await db.CleaningReworks.AsNoTracking().AnyAsync(w=>w.InspectionId==inspection&&w.Status!="COMPLETED"&&w.Status!="CANCELLED",ct))
            throw new InvalidOperationException("REWORK_CURRENT_FAILED_SOURCE_REQUIRED");
        if(await db.InspectionDefects.AsNoTracking().CountAsync(d=>d.InspectionId==inspection&&defects.Contains(d.DefectId)
            && db.InspectionResults.Any(r=>r.InspectionId==inspection&&r.ChecklistItemId==d.ChecklistItemId&&r.Outcome=="FAIL"
                && r.ChecklistItem.VersionId==d.Inspection.VersionId),ct)!=defects.Count)
            throw new InvalidOperationException("REWORK_DEFECT_SOURCE_INVALID");
        var ids=await db.Database.SqlQuery<long>($"""
            SET NOCOUNT ON;
            INSERT dbo.cleaning_reworks(inspection_id,cleaning_task_id,assignee_user_id,reason,status,workflow_status,due_at,requested_at)
            VALUES({inspection},{sources[0].TaskId},{assignee},{reason.Trim()},'PENDING','PENDING_ACCEPTANCE',{due},SYSUTCDATETIME());
            SELECT CONVERT(bigint,SCOPE_IDENTITY()) AS Value;
            """).ToArrayAsync(ct);
        if(ids.Length!=1||ids[0]<=0)throw new InvalidOperationException("REWORK_IDENTITY_REQUIRED");
        foreach(var defect in defects.Order())
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.cleaning_rework_defects(rework_id,defect_id) VALUES({ids[0]},{defect})",ct);
        return ids[0];
    }
    private sealed class FailedRow {public long TaskId{get;set;}public long RoomId{get;set;}}
}
