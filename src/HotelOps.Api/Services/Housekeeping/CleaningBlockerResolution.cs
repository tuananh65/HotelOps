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

    private async Task<string?> ResolveCleaningBlockers(long task, long branch, long actor, string note, DateTime now, CancellationToken ct)
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
        // No accepted source confirmation reader exists yet; fail closed for linked blockers,
        // including an imported resolved flag without source confirmation proof.
        if (blockers.Any(x => x.IncidentId != null || x.MaterialRequestId != null)) return "BLOCKER_SOURCE_CONFIRMATION_REQUIRED";
        var open = blockers.Where(x => x.ResolvedAt == null).ToArray();
        if (blockers.Length == 0) return "NO_PERSISTED_BLOCKER";
        foreach (var blocker in open)
        {
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE dbo.fs014_cleaning_blocker_resolutions SET resolved_by={actor},resolved_at={now},resolution_note={note}
                WHERE progress_id={blocker.ProgressId} AND resolved_at IS NULL
                """, ct);
            if (affected != 1) return "BLOCKER_STATE_CHANGED";
            await BatchAuditAsync(actor, branch, "cleaning_progress_logs", blocker.ProgressId,
                new { resolvedAt = (DateTime?)null }, new { action = "RESOLVE_CLEANING_BLOCKER", task, resolutionNote = note,
                    resolvedAt = now, resolvedBy = actor, contract = "PROVISIONAL_V1" }, ["blockerResolution"], ct);
        }
        return null;
    }
}
