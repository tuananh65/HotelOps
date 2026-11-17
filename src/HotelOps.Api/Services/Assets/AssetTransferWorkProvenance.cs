using HotelOps.Api.Entities;
using HotelOps.Api.Services.Insights;
using HotelOps.Api.Services.PreventiveMaintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetCrossBranchTransfer
{
    // Called only inside the acceptance transaction, before changing Asset.BranchId.
    // Existing provenance is never rewritten on subsequent transfers.
    private async Task<object> CaptureWorkAsync(Asset asset, long handover, long audit, DateTime now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Acceptance transaction required.");
        var incidents = await db.Incidents.AsNoTracking().Where(i => i.AssetId == asset.AssetId)
            .Select(i => new { i.IncidentId, i.BranchId, i.Status }).ToArrayAsync(ct);
        var runs = await db.PreventiveMaintenanceRuns.AsNoTracking().Where(r => r.Schedule.AssetId == asset.AssetId)
            .Select(r => new { r.RunId, r.ScheduleId, r.MaintenanceWorkId }).ToArrayAsync(ct);
        var existing = await db.AssetWorkSourceScopes.AsNoTracking().Where(s => s.AssetId == asset.AssetId).ToArrayAsync(ct);
        var schedules = await db.PreventiveMaintenanceSchedules.AsNoTracking().Where(s => s.AssetId == asset.AssetId)
            .Select(s => s.ScheduleId).ToArrayAsync(ct);
        foreach (var schedule in schedules)
        {
            if (await db.AssetScheduleSourceScopes.AnyAsync(s => s.ScheduleId == schedule && s.AssetId == asset.AssetId, ct)) continue;
            var origin = await db.Database.SqlQuery<long>($"""
                SELECT a.branch_id AS Value FROM dbo.audit_logs a WITH(HOLDLOCK)
                WHERE a.entity_type='preventive_maintenance_schedules' AND a.entity_id=CONVERT(varchar(30),{schedule})
                  AND a.action='CREATE' AND a.old_values IS NULL AND a.branch_id IS NOT NULL AND ISJSON(a.new_values)=1
                  AND TRY_CONVERT(bigint,JSON_VALUE(a.new_values,'$.ScheduleId'))={schedule}
                  AND TRY_CONVERT(bigint,JSON_VALUE(a.new_values,'$.AssetId'))={asset.AssetId}
                """).ToArrayAsync(ct);
            if (origin.Length != 1 || origin[0] != asset.BranchId) throw InconsistentWork();
            db.AssetScheduleSourceScopes.Add(new AssetScheduleSourceScope { ScheduleId = schedule, AssetId = asset.AssetId,
                BranchId = asset.BranchId, HandoverId = handover, AuditId = audit, CapturedAt = now });
        }
        foreach (var incident in incidents)
        {
            var proof = existing.SingleOrDefault(s => s.IncidentId == incident.IncidentId);
            if (proof != null)
            {
                if (proof.BranchId != incident.BranchId) throw InconsistentWork();
                continue;
            }
            if (incident.BranchId != asset.BranchId) throw InconsistentWork();
            db.AssetWorkSourceScopes.Add(new AssetWorkSourceScope
            {
                AssetId = asset.AssetId, BranchId = incident.BranchId, IncidentId = incident.IncidentId,
                HandoverId = handover, AuditId = audit, AssetCode = asset.AssetCode, AssetName = asset.AssetName, CapturedAt = now
            });
        }
        if (runs.Length > 0 && !await PreventiveOccurrenceGeneration.InstalledAsync(db, ct)) throw InconsistentWork();
        foreach (var run in runs)
        {
            // The generation receipt, not the asset's present branch, establishes occurrence ownership.
            var branch = await db.Database.SqlQuery<long>($"""
                SELECT d.branch_id AS Value FROM dbo.preventive_occurrence_details d WITH(HOLDLOCK)
                JOIN dbo.audit_logs a WITH(HOLDLOCK) ON a.audit_id=d.generation_audit_id
                WHERE d.run_id={run.RunId} AND a.entity_type='preventive_maintenance_runs'
                  AND a.entity_id=CONVERT(varchar(30),d.run_id) AND a.branch_id=d.branch_id AND a.action='CREATE'
                  AND a.occurred_at=d.generated_at AND ISJSON(d.definition_json)=1
                  AND TRY_CONVERT(bigint,JSON_VALUE(d.definition_json,'$.assetId'))={asset.AssetId}
                  AND TRY_CONVERT(bigint,JSON_VALUE(d.definition_json,'$.scheduleId'))={run.ScheduleId}
                """).SingleOrDefaultAsync(ct);
            if (branch <= 0) throw InconsistentWork();
            var proof = existing.SingleOrDefault(s => s.RunId == run.RunId);
            if (proof != null)
            {
                if (proof.BranchId != branch) throw InconsistentWork();
                continue;
            }
            if (branch != asset.BranchId) throw InconsistentWork();
            db.AssetWorkSourceScopes.Add(new AssetWorkSourceScope
            {
                AssetId = asset.AssetId, BranchId = branch, RunId = run.RunId,
                HandoverId = handover, AuditId = audit, AssetCode = asset.AssetCode, AssetName = asset.AssetName, CapturedAt = now
            });
        }
        await db.SaveChangesAsync(ct);
        return new { Incidents = incidents, Occurrences = runs, Schedules = schedules };
    }

    private static InsightException InconsistentWork() => new(409, "SOURCE_WORK_PROVENANCE_REQUIRED",
        "Linked work requires consistent immutable source-branch evidence before custody can move.");
}
