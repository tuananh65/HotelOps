using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal static class TaskSeriesRequestLink
{
    internal static async Task<bool> CancelledOccurrenceProvenAsync(HotelOpsDbContext db,long request,long task,long branch,CancellationToken ct)
    {
        if(!await TaskSeriesStore.InstalledAsync(db,ct))return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.task_series_occurrences o
            JOIN dbo.operational_task_series s ON s.series_id=o.series_id AND s.branch_id={branch}
            JOIN dbo.operational_tasks t ON t.task_id=o.task_id AND t.branch_id=s.branch_id
            JOIN dbo.task_progress_versions v ON v.task_id=t.task_id AND v.workflow_status='CANCELLED'
            JOIN dbo.audit_logs a ON a.entity_type='operational_tasks' AND a.entity_id=CONVERT(nvarchar(30),t.task_id)
              AND a.branch_id=t.branch_id AND a.action='UPDATE' AND a.occurred_at=t.updated_at
            CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
            JOIN dbo.operational_task_progress p ON p.progress_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.progressId'))
              AND p.task_id=t.task_id AND p.new_status='CANCELLED' AND p.reported_by=a.actor_user_id AND p.reported_at=a.occurred_at
            WHERE t.task_id={task} AND t.status='CANCELLED' AND t.actual_start_at IS NULL AND t.completed_at IS NULL
              AND t.source_type='INTERNAL_REQUEST' AND t.source_id={request}
              AND TRY_CONVERT(bigint,JSON_VALUE(s.template_json,'$.InternalRequestId'))={request}
              AND JSON_VALUE(j.payload,'$.operation')='CANCEL_TASK' AND JSON_VALUE(j.payload,'$.workflowState')='CANCELLED'
              AND LEN(LTRIM(RTRIM(JSON_VALUE(j.payload,'$.reason'))))>=10
              AND p.description=CONCAT(N'Task cancelled: ',JSON_VALUE(j.payload,'$.reason'))
              AND a.occurred_at>=o.created_at AND a.occurred_at<=SYSUTCDATETIME()
              AND NOT EXISTS(SELECT 1 FROM dbo.work_intervals i WHERE i.work_item_kind='TASK' AND i.work_item_id=t.task_id)
              AND NOT EXISTS(SELECT 1 FROM dbo.operational_task_assignments x WHERE x.task_id=t.task_id AND (x.assignment_status='ACTIVE' OR x.ended_at IS NULL))
              AND (JSON_VALUE(j.payload,'$.assignmentId') IS NULL OR EXISTS(
                SELECT 1 FROM dbo.operational_task_assignments x WHERE x.assignment_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assignmentId'))
                  AND x.task_id=t.task_id AND x.assignment_status='CANCELLED' AND x.ended_at=a.occurred_at))
            """).SingleAsync(ct)==1;
    }

    internal static async Task<bool> AllowedAsync(HotelOpsDbContext db,long actor,long branch,long request,DateTime now,CancellationToken ct)
    {
        if(!await InternalRequestDecisionStore.InstalledAsync(db,ct)||!await InternalRequestTaskLinks.InstalledAsync(db,ct)
            ||!await new InternalRequestProcessAuthority(db).AllowsCurrentRequestAsync(actor,request,ct))return false;
        var scope=await new InternalRequestReadAccess(db).ReadAsync(actor,now,ct);
        return await scope.Apply(db.InternalRequests.AsNoTracking(),now).AnyAsync(r=>r.InternalRequestId==request&&r.BranchId==branch
            &&(r.Status=="ACCEPTED"||r.Status=="IN_PROGRESS"),ct);
    }

    // Called by terminal IR workflows in their existing serializable transaction.
    // Only future generation changes; occurrence links and execution history are untouched.
    internal static async Task StopAsync(HotelOpsDbContext db,long request,long actor,DateTime now,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction==null)throw new InvalidOperationException("IR series stop requires a transaction.");
        if(!await db.Database.SqlQuery<int>($"SELECT CASE WHEN OBJECT_ID('dbo.operational_task_series','U') IS NULL THEN 0 ELSE 1 END AS Value").AnyAsync(x=>x==1,ct))return;
        var ids=await db.Database.SqlQuery<long>($"""
            SELECT series_id AS Value FROM dbo.operational_task_series WITH(UPDLOCK,HOLDLOCK)
            WHERE is_active=1 AND TRY_CONVERT(bigint,JSON_VALUE(template_json,'$.InternalRequestId'))={request}
            """).ToArrayAsync(ct);
        foreach(var id in ids)
        {
            var row=await TaskSeriesStore.Query(db).SingleAsync(s=>s.SeriesId==id,ct);
            if(TaskSeriesStore.Template(row).InternalRequestId!=request)throw new InvalidOperationException("Series source mismatch.");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_series SET is_active=0,stopped_at={now},stopped_by={actor} WHERE series_id={id} AND is_active=1",ct);
            db.AuditLogs.Add(new AuditLog{ActorUserId=actor,BranchId=row.BranchId,EntityType="operational_task_series",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues="{\"isActive\":true}",NewValues=JsonSerializer.Serialize(new{operation="STOP_TERMINAL_IR",internalRequestId=request,isActive=false}),ChangedFields="[\"isActive\",\"stoppedAt\",\"stoppedBy\"]"});
        }
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
    }
}
