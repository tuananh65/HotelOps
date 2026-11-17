using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class BranchDeactivationDependencyReader
{
    private sealed class HousekeepingFact
    {
        public string Kind {get;set;}="";
        public string? Status {get;set;}
        public string Classification {get;set;}="";
        public long Count {get;set;}
    }
    private async Task<BranchDependencyState[]> HousekeepingFacts(long branch,CancellationToken ct)
    {
        if(await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN COL_LENGTH('dbo.cleaning_tasks','workflow_status') IS NOT NULL
              AND COL_LENGTH('dbo.cleaning_reworks','workflow_status') IS NOT NULL
              AND COL_LENGTH('dbo.room_inspections','workflow_status') IS NOT NULL
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)!=1)
            return [new("housekeeping","WORKFLOW_SCHEMA_REQUIRED",1,"UNRESOLVED")];
        // Physical COMPLETED does not imply completion of the cleaning/rework workflow:
        // a failed inspection/reinspection can leave the cleaning cycle open.
        var rows=await db.Database.SqlQuery<HousekeepingFact>($"""
            SELECT Kind,Status,Classification,COUNT_BIG(*) AS Count FROM (
              SELECT 'cleaning_tasks' AS Kind,t.workflow_status AS Status,
                CASE WHEN t.workflow_status='COMPLETED' AND t.status='COMPLETED' AND t.actual_end_at IS NOT NULL THEN 'CLOSED'
                  WHEN t.workflow_status='CANCELLED' AND t.status='CANCELLED' THEN 'CLOSED'
                  WHEN (t.workflow_status IN('PENDING_ASSIGNMENT','ASSIGNED') AND t.status='PENDING')
                    OR (t.workflow_status IN('IN_PROGRESS','ON_HOLD','PENDING_INSPECTION','REWORK_REQUIRED') AND t.status='IN_PROGRESS') THEN 'OPEN'
                  ELSE 'UNRESOLVED' END AS Classification
              FROM dbo.cleaning_tasks t JOIN dbo.rooms r ON r.room_id=t.room_id WHERE r.branch_id={branch}
              UNION ALL
              SELECT 'cleaning_reworks',w.workflow_status,
                CASE WHEN w.workflow_status IN('COMPLETED','FAILED') AND w.status='COMPLETED' AND w.completed_at IS NOT NULL THEN 'CLOSED'
                  WHEN w.workflow_status='CANCELLED' AND w.status='CANCELLED' THEN 'CLOSED'
                  WHEN (w.workflow_status IN('PENDING_ACCEPTANCE','ACCEPTED') AND w.status='PENDING')
                    OR (w.workflow_status IN('IN_PROGRESS','ON_HOLD','PENDING_REINSPECTION') AND w.status='IN_PROGRESS') THEN 'OPEN'
                  ELSE 'UNRESOLVED' END
              FROM dbo.cleaning_reworks w JOIN dbo.cleaning_tasks t ON t.cleaning_task_id=w.cleaning_task_id
              JOIN dbo.rooms r ON r.room_id=t.room_id WHERE r.branch_id={branch}
              UNION ALL
              SELECT 'room_inspections',i.workflow_status,
                CASE WHEN i.status='COMPLETED' AND i.inspected_at IS NOT NULL
                    AND ((i.workflow_status='PASSED' AND i.result='PASS') OR (i.workflow_status='FAILED' AND i.result='FAIL')) THEN 'CLOSED'
                  WHEN i.workflow_status='CANCELLED' AND i.status='CANCELLED' THEN 'CLOSED'
                  WHEN ((i.workflow_status='ASSIGNED' AND i.status='PENDING')
                    OR (i.workflow_status IN('INSPECTING','PENDING_CONCLUSION') AND i.status='IN_PROGRESS')) AND i.result IS NULL THEN 'OPEN'
                  ELSE 'UNRESOLVED' END
              FROM dbo.room_inspections i JOIN dbo.rooms r ON r.room_id=i.room_id
              WHERE r.branch_id={branch}
            ) states GROUP BY Kind,Status,Classification
            """).ToArrayAsync(ct);
        return rows.Select(r=>new BranchDependencyState(r.Kind,r.Status,r.Count,r.Classification)).ToArray();
    }
}
