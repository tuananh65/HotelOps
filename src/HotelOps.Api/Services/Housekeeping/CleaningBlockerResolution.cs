using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private sealed class ResumeBlocker
    {
        public long ProgressId { get; set; }
        public long? IncidentId { get; set; }
        public long? MaterialRequestId { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    private async Task<bool> ResumeTrialInstalled(CancellationToken ct) =>
        await HotelOps.Api.Data.CleaningResumeSchema.InstalledAsync(db, ct) || await LegacyResumeTrialInstalled(ct);

    private async Task<bool> LegacyResumeTrialInstalled(CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN ((DB_NAME()='HotelOps_Canonical_FS014_Resume_V1_Fresh'
        AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS014.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS014'))
        OR (DB_NAME()='HotelOps_Canonical_FS015_Cleaning_V1_Fresh'
        AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS015.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS015'))
        OR (DB_NAME()='HotelOps_Canonical_FS016_Rework_V1_Fresh'
        AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS016.Owner' AND CONVERT(nvarchar(128),value)='DEV1_FS016')))
        AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS014.Resume' AND CONVERT(nvarchar(128),value)='PROVISIONAL_V1')
        AND OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions','U') IS NOT NULL
        AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_fs014_blocker_version') AND is_disabled=0)
        AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions') AND is_disabled=0 AND is_not_trusted=0)=2
        AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions') AND is_disabled=0 AND is_not_trusted=0)=2
        THEN 1 ELSE 0 END AS [Value]
        """).SingleAsync(ct) == 1;

    private async Task<string?> ResolveCleaningBlockers(long task, long branch, long actor, string note, DateTime now, CancellationToken ct, bool endOfShift = false)
    {
        if (!await ResumeTrialInstalled(ct)) return "BLOCKER_RESOLUTION_CONTRACT_REQUIRED";
        // Every historical BLOCKED source must have explicit durable state. Never infer closure
        // from another progress entry, task state, client assertion, or absence of records.
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM dbo.cleaning_progress_logs p WITH(UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.fs014_cleaning_blocker_resolutions b WITH(UPDLOCK,HOLDLOCK) ON b.progress_id=p.progress_id
            WHERE p.cleaning_task_id={task} AND p.status='BLOCKED' AND (b.progress_id IS NULL OR p.blocker_type_id IS NULL
            OR NOT EXISTS(SELECT 1 FROM dbo.reference_values r WHERE r.reference_id=p.blocker_type_id AND r.group_code='BLOCKER'))
            """).SingleAsync(ct) != 0) return "BLOCKER_STATE_UNRECONCILED";
        var blockers = await db.Database.SqlQuery<ResumeBlocker>($"""
            SELECT b.progress_id AS ProgressId,b.incident_id AS IncidentId,b.material_request_id AS MaterialRequestId,b.resolved_at AS ResolvedAt
            FROM dbo.fs014_cleaning_blocker_resolutions b WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.cleaning_progress_logs p WITH(UPDLOCK,HOLDLOCK) ON p.progress_id=b.progress_id
            WHERE p.cleaning_task_id={task} AND p.status='BLOCKED'
            """).ToArrayAsync(ct);
        var room=await db.CleaningTasks.AsNoTracking().Where(t=>t.CleaningTaskId==task).Select(t=>t.RoomId).SingleAsync(ct);
        var proofs=new Dictionary<long,CleaningBlockerProof>();
        foreach(var blocker in blockers)
        {
            var proof=await CleaningBlockerProofAsync(blocker,actor,branch,task,room,ct);
            if(proof.Failure!=null)return proof.Failure;
            proofs.Add(blocker.ProgressId,proof);
        }
        var open = blockers.Where(x => x.ResolvedAt == null).ToArray();
        // Ending a shift never proves that an actual blocker has been resolved.
        if (endOfShift) return open.Length == 0 ? null : "ACTIVE_BLOCKER";
        if (blockers.Length == 0) return "NO_PERSISTED_BLOCKER";
        foreach (var blocker in open)
        {
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.fs014_cleaning_blocker_resolutions SET resolved_by={actor},resolved_at={now},resolution_note={note}
                WHERE progress_id={blocker.ProgressId} AND resolved_at IS NULL
                """, ct);
            if (affected != 1) return "BLOCKER_STATE_CHANGED";
            db.AuditLogs.Add(new HotelOps.Api.Entities.AuditLog {
                ActorUserId=actor,BranchId=branch,EntityType="cleaning_progress_logs",
                EntityId=blocker.ProgressId.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues="{\"resolvedAt\":null}",
                NewValues=System.Text.Json.JsonSerializer.Serialize(new { action="RESOLVE_CLEANING_BLOCKER",task,resolutionNote=note,
                    resolvedAt=now,resolvedBy=actor,incidentId=blocker.IncidentId,materialRequestId=blocker.MaterialRequestId,
                    receipts=proofs[blocker.ProgressId].Receipt,contract="EXPLICIT_HOUSEKEEPER_CONFIRMATION" }),
                ChangedFields="[\"blockerResolution\"]" });
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        }
        await BatchAuditAsync(actor,branch,"cleaning_tasks",task,null,
            new {action="CONFIRM_CLEANING_BLOCKERS",confirmedBy=actor,confirmedAt=now,note,
                blockers=blockers.Select(b=>new{b.ProgressId,b.IncidentId,b.MaterialRequestId,receipts=proofs[b.ProgressId].Receipt}).ToArray()},
            ["blockerConfirmation"],ct);
        return null;
    }
}
