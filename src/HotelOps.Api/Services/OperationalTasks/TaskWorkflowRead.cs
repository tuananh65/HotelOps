using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class TaskWorkflowRow
{
    public long TaskId { get; set; }
    public string State { get; set; } = "";
}

internal static class TaskWorkflowRead
{
    internal static readonly string[] States = ["PENDING_ASSIGNMENT", "ASSIGNED", "ACCEPTED", "IN_PROGRESS", "PAUSED", "PENDING_VERIFICATION", "CLOSED"];
    // Read the same authority as mutations. Never infer Assigned from legacy TODO.
    internal static IQueryable<TaskWorkflowRow> Query(HotelOpsDbContext db, DateTime now) => db.Database.SqlQuery<TaskWorkflowRow>($"""
        SELECT v.task_id AS TaskId,v.workflow_status AS State FROM dbo.task_progress_versions v
        JOIN dbo.operational_tasks t ON t.task_id=v.task_id
        WHERE (v.workflow_status IN('ASSIGNED','ACCEPTED') AND t.status='TODO' AND t.completed_at IS NULL
            AND (SELECT COUNT(*) FROM dbo.operational_task_assignments x WHERE x.task_id=t.task_id AND x.assignment_status='ACTIVE' AND x.ended_at IS NULL AND x.assigned_at<={now})=1
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskEntry.V1' AND CONVERT(nvarchar(20),value)='1'))
          OR (v.workflow_status IN('IN_PROGRESS','PAUSED') AND t.status='IN_PROGRESS' AND t.completed_at IS NULL)
          OR (v.workflow_status='PENDING_ASSIGNMENT' AND t.status='TODO' AND t.completed_at IS NULL
            AND NOT EXISTS(SELECT 1 FROM dbo.operational_task_assignments x WHERE x.task_id=t.task_id AND x.assignment_status='ACTIVE' AND x.ended_at IS NULL)
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskDecline.V1' AND CONVERT(nvarchar(20),value)='1'))
          OR (v.workflow_status='PENDING_VERIFICATION' AND t.status='IN_PROGRESS' AND t.completed_at IS NULL
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSubmission.V1' AND CONVERT(nvarchar(20),value)='1'))
          OR (v.workflow_status='CLOSED' AND t.status='COMPLETED' AND t.completed_at IS NOT NULL
            AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskVerification.V1' AND CONVERT(nvarchar(20),value)='1'))
        """);
}
