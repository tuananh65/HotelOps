using HotelOps.Api.Services.PreventiveMaintenance;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class BranchDeactivationDependencyReader
{
    private sealed class PreventiveFact
    {
        public string? State {get;set;}
        public int ProvenTerminal {get;set;}
        public bool ScopeKnown {get;set;}
        public bool OpenMaintenance {get;set;}
    }
    private async Task<BranchDependencyState[]> PreventiveFacts(long branch,CancellationToken ct)
    {
        if(!await db.PreventiveMaintenanceRuns.AnyAsync(ct))return [];
        if(!await PreventiveOccurrenceGeneration.InstalledAsync(db,ct)
            ||!await PreventiveOccurrenceExecutionService.CompletionInstalledAsync(db,ct))
            return [new("preventive_occurrences","PROVENANCE_CAPABILITY_REQUIRED",1,"UNRESOLVED")];
        // The persisted occurrence branch is authoritative. Legacy runs without a snapshot
        // are flagged, not adopted using an employee's current membership.
        var rows=await db.Database.SqlQuery<PreventiveFact>($"""
            SELECT d.workflow_status AS State,CONVERT(bit,CASE WHEN d.branch_id IS NOT NULL THEN 1 ELSE 0 END) AS ScopeKnown,
              CONVERT(bit,CASE WHEN w.maintenance_work_id IS NOT NULL AND w.status NOT IN('CLOSED','CANCELLED') THEN 1 ELSE 0 END) AS OpenMaintenance,
              CASE WHEN d.workflow_status='COMPLETED' AND EXISTS(
                SELECT 1 FROM dbo.preventive_occurrence_completions c
                JOIN dbo.audit_logs a ON a.audit_id=c.audit_id
                JOIN dbo.work_intervals wi ON wi.interval_id=c.interval_id
                WHERE c.run_id=r.run_id AND c.completed_at=r.performed_at AND c.actor_id=r.performed_by
                  AND a.entity_type='preventive_maintenance_runs' AND a.entity_id=CONVERT(varchar(30),r.run_id)
                  AND a.branch_id=d.branch_id AND a.actor_user_id=c.actor_id AND a.occurred_at=c.completed_at
                  AND wi.work_item_kind='OCCURRENCE' AND wi.work_item_id=r.run_id AND wi.branch_id=d.branch_id
                  AND wi.user_id=c.actor_id AND wi.closed_by=c.actor_id AND wi.closed_at=c.completed_at
                  AND NOT EXISTS(SELECT 1 FROM dbo.work_intervals o WHERE o.work_item_kind='OCCURRENCE' AND o.work_item_id=r.run_id AND o.closed_at IS NULL)) THEN 1
                WHEN d.workflow_status='CANCELLED' AND EXISTS(
                  SELECT 1 FROM dbo.audit_logs a WHERE a.entity_type='preventive_maintenance_runs'
                    AND a.entity_id=CONVERT(varchar(30),r.run_id) AND a.action='UPDATE' AND a.branch_id=d.branch_id
                    AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE '[]' END,'$.eventType')='PREVENTIVE_OCCURRENCE_CANCELLED'
                    AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE '[]' END,'$.status')='CANCELLED'
                    AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE '[]' END,'$.runId'))=r.run_id
                    AND JSON_VALUE(CASE WHEN ISJSON(a.old_values)=1 THEN a.old_values ELSE '[]' END,'$.status') IN('PENDING_ASSIGNMENT','ASSIGNED')
                    AND LEN(LTRIM(RTRIM(JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE '[]' END,'$.reason'))))>=10
                    AND a.actor_user_id>0 AND a.occurred_at<=SYSUTCDATETIME())
                  AND NOT EXISTS(SELECT 1 FROM dbo.work_intervals o WHERE o.work_item_kind='OCCURRENCE' AND o.work_item_id=r.run_id AND o.closed_at IS NULL)
                THEN 1 ELSE 0 END AS ProvenTerminal
            FROM dbo.preventive_maintenance_runs r
            JOIN dbo.preventive_maintenance_schedules s ON s.schedule_id=r.schedule_id
            LEFT JOIN dbo.preventive_occurrence_details d ON d.run_id=r.run_id
            LEFT JOIN dbo.maintenance_works w ON w.maintenance_work_id=r.maintenance_work_id
            LEFT JOIN dbo.assets a ON a.asset_id=s.asset_id
            LEFT JOIN dbo.rooms room ON room.room_id=s.room_id
            LEFT JOIN dbo.areas area ON area.area_id=s.area_id
            WHERE d.branch_id={branch} OR (d.run_id IS NULL AND
              (a.branch_id={branch} OR room.branch_id={branch} OR area.branch_id={branch}
               OR (a.branch_id IS NULL AND room.branch_id IS NULL AND area.branch_id IS NULL)))
            """).ToArrayAsync(ct);
        return rows.Select(r=>new {Status=r.OpenMaintenance?"LINKED_MAINTENANCE_OPEN":r.State??"MISSING_SNAPSHOT",Classification=!r.ScopeKnown?"UNRESOLVED":r.OpenMaintenance?"OPEN":r.ProvenTerminal==1?"CLOSED":
                r.State is "PENDING_ASSIGNMENT" or "ASSIGNED" or "IN_PROGRESS" or "ON_HOLD"?"OPEN":"UNRESOLVED"})
            .GroupBy(r=>new{r.Status,r.Classification})
            .Select(g=>new BranchDependencyState("preventive_occurrences",g.Key.Status,g.LongCount(),g.Key.Classification)).ToArray();
    }
}
