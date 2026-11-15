using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskPlanEvent
{
    public long AuditId { get; set; }
    public long TaskId { get; set; }
    public long BranchId { get; set; }
    public long DepartmentId { get; set; }
    public long ActorId { get; set; }
    public long? RecipientId { get; set; }
    public DateTime At { get; set; }
    public string Operation { get; set; } = "";
    public string NotificationKind { get; set; } = "";
    public long? PreviousPriorityId { get; set; }
    public DateTime? PreviousPlannedStartAt { get; set; }
    public DateTime? PreviousDueAt { get; set; }
    public int? PreviousSlaMinutes { get; set; }
    public long? PriorityId { get; set; }
    public DateTime? PlannedStartAt { get; set; }
    public DateTime? DueAt { get; set; }
    public int? SlaMinutes { get; set; }
    public string? Reason { get; set; }
}
internal static class TaskPlanNotificationStore
{
    internal static IQueryable<StoredTaskPlanEvent> Query(HotelOpsDbContext db) => db.Database.SqlQuery<StoredTaskPlanEvent>($"""
        SELECT a.audit_id AS AuditId,t.task_id AS TaskId,t.branch_id AS BranchId,t.department_id AS DepartmentId,
          a.actor_user_id AS ActorId,x.assignee_id AS RecipientId,a.occurred_at AS At,
          JSON_VALUE(j.payload,'$.operation') AS Operation,JSON_VALUE(j.payload,'$.notificationKind') AS NotificationKind,
          TRY_CONVERT(bigint,JSON_VALUE(j.before,'$.priorityId')) AS PreviousPriorityId,
          TRY_CONVERT(datetime2,JSON_VALUE(j.before,'$.plannedStartAt')) AS PreviousPlannedStartAt,
          TRY_CONVERT(datetime2,JSON_VALUE(j.before,'$.dueAt')) AS PreviousDueAt,
          TRY_CONVERT(int,JSON_VALUE(j.before,'$.slaMinutes')) AS PreviousSlaMinutes,
          TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.priorityId')) AS PriorityId,
          TRY_CONVERT(datetime2,JSON_VALUE(j.payload,'$.plannedStartAt')) AS PlannedStartAt,
          TRY_CONVERT(datetime2,JSON_VALUE(j.payload,'$.dueAt')) AS DueAt,
          TRY_CONVERT(int,JSON_VALUE(j.payload,'$.slaMinutes')) AS SlaMinutes,
          JSON_VALUE(j.payload,'$.reason') AS Reason
        FROM dbo.audit_logs a
        CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload,
          CASE WHEN ISJSON(a.old_values)=1 THEN a.old_values ELSE N'[]' END AS before) j
        JOIN dbo.operational_tasks t ON a.entity_type='operational_tasks' AND a.entity_id=CONVERT(nvarchar(50),t.task_id)
          AND a.branch_id=t.branch_id AND a.action='UPDATE'
        JOIN dbo.operational_task_progress p ON p.progress_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.progressId'))
          AND p.task_id=t.task_id AND p.reported_by=a.actor_user_id AND p.reported_at=a.occurred_at
        LEFT JOIN dbo.operational_task_assignments x ON x.assignment_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assignmentId'))
          AND x.task_id=t.task_id AND x.assignee_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.recipientId'))
          AND x.assigned_at<=a.occurred_at AND (x.ended_at IS NULL OR x.ended_at>=a.occurred_at)
        WHERE ((JSON_VALUE(j.payload,'$.operation')='PLAN' AND JSON_VALUE(j.payload,'$.notificationKind')='TASK_PLANNED'
            AND p.old_status=p.new_status AND p.new_status IN('TODO','IN_PROGRESS')
            AND p.description=CONCAT(N'Task plan updated.',CASE WHEN JSON_VALUE(j.payload,'$.reason') IS NULL THEN N'' ELSE CONCAT(N' ',JSON_VALUE(j.payload,'$.reason')) END))
          OR (JSON_VALUE(j.payload,'$.operation')='CANCEL_TASK' AND JSON_VALUE(j.payload,'$.notificationKind')='TASK_CANCELLED'
            AND JSON_VALUE(j.payload,'$.workflowState')='CANCELLED' AND p.old_status='TODO' AND p.new_status='CANCELLED'
            AND p.description=CONCAT(N'Task cancelled: ',JSON_VALUE(j.payload,'$.reason'))
            AND LEN(LTRIM(RTRIM(JSON_VALUE(j.payload,'$.reason'))))>=10
            AND (x.assignment_id IS NULL OR x.assignment_status='CANCELLED' AND x.ended_at=a.occurred_at)))
          AND ((JSON_VALUE(j.payload,'$.assignmentId') IS NULL AND JSON_VALUE(j.payload,'$.recipientId') IS NULL) OR x.assignment_id IS NOT NULL)
          AND EXISTS(SELECT 1 FROM dbo.departments d WHERE d.department_id=t.department_id AND d.branch_id=t.branch_id)
          AND (t.room_id IS NULL OR EXISTS(SELECT 1 FROM dbo.rooms r WHERE r.room_id=t.room_id AND r.branch_id=t.branch_id))
          AND (t.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas r WHERE r.area_id=t.area_id AND r.branch_id=t.branch_id))
        """);
}
