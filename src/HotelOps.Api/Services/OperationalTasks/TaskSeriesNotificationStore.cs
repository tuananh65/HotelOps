using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskSeriesStop
{
    public long AuditId { get; set; }
    public long SeriesId { get; set; }
    public long TaskId { get; set; }
    public long BranchId { get; set; }
    public long DepartmentId { get; set; }
    public long ActorId { get; set; }
    public long RecipientId { get; set; }
    public DateTime At { get; set; }
}
internal static class TaskSeriesNotificationStore
{
    internal static IQueryable<StoredTaskSeriesStop> Query(HotelOpsDbContext db) => db.Database.SqlQuery<StoredTaskSeriesStop>($"""
        SELECT a.audit_id AS AuditId,s.series_id AS SeriesId,t.task_id AS TaskId,t.branch_id AS BranchId,
          t.department_id AS DepartmentId,a.actor_user_id AS ActorId,x.assignee_id AS RecipientId,a.occurred_at AS At
        FROM dbo.audit_logs a
        CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
        JOIN dbo.operational_task_series s ON a.entity_type='operational_task_series' AND a.entity_id=CONVERT(nvarchar(50),s.series_id)
          AND a.branch_id=s.branch_id AND a.action='UPDATE' AND s.is_active=0 AND s.stopped_by=a.actor_user_id AND s.stopped_at=a.occurred_at
        JOIN dbo.task_series_occurrences o ON o.series_id=s.series_id AND o.task_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.taskId'))
        JOIN dbo.operational_tasks t ON t.task_id=o.task_id AND t.branch_id=s.branch_id AND t.department_id=s.department_id
        JOIN dbo.operational_task_assignments x ON x.assignment_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assignmentId'))
          AND x.task_id=t.task_id AND x.assignee_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.recipientId'))
          AND x.assigned_at<=a.occurred_at AND (x.ended_at IS NULL OR x.ended_at>=a.occurred_at)
        WHERE JSON_VALUE(j.payload,'$.operation')='STOP_SERIES' AND JSON_VALUE(j.payload,'$.isActive')='false'
          AND JSON_VALUE(j.payload,'$.notificationKind')='TASK_SERIES_STOPPED'
          AND EXISTS(SELECT 1 FROM dbo.departments d WHERE d.department_id=t.department_id AND d.branch_id=t.branch_id)
        """);
}
