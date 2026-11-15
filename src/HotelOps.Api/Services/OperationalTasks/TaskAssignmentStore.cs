using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskAssignment
{
    public long AuditId { get; set; }
    public long TaskId { get; set; }
    public long BranchId { get; set; }
    public long DepartmentId { get; set; }
    public long AssignedBy { get; set; }
    public long AssigneeId { get; set; }
    public long? PreviousAssigneeId { get; set; }
    public DateTime AssignedAt { get; set; }
}

internal static class TaskAssignmentStore
{
    internal const string NotificationKind = "TASK_ASSIGNED";
    // Audit + physical assignment/progress provenance survive later reassignments.
    // A notification or a JSON claim alone never grants access to the task.
    internal static IQueryable<StoredTaskAssignment> Query(HotelOpsDbContext db) => db.Database.SqlQuery<StoredTaskAssignment>($"""
        SELECT a.audit_id AS AuditId,t.task_id AS TaskId,t.branch_id AS BranchId,t.department_id AS DepartmentId,
          a.actor_user_id AS AssignedBy,x.assignee_id AS AssigneeId,old.assignee_id AS PreviousAssigneeId,a.occurred_at AS AssignedAt
        FROM dbo.audit_logs a
        CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
        JOIN dbo.operational_tasks t ON a.entity_type='operational_tasks' AND a.entity_id=CONVERT(nvarchar(50),t.task_id)
          AND a.branch_id=t.branch_id AND a.action='UPDATE'
        JOIN dbo.operational_task_assignments x ON x.assignment_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assignmentId'))
          AND x.task_id=t.task_id AND x.assignee_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assigneeId'))
          AND x.assigned_by=a.actor_user_id AND x.assigned_at=a.occurred_at
        JOIN dbo.operational_task_progress p ON p.progress_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.progressId'))
          AND p.task_id=t.task_id AND p.reported_by=a.actor_user_id AND p.reported_at=a.occurred_at AND p.new_status='TODO'
        LEFT JOIN dbo.operational_task_assignments old ON old.assignment_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.previousAssignmentId'))
          AND old.task_id=t.task_id AND old.assignee_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.previousAssigneeId'))
          AND old.assignee_id<>x.assignee_id AND old.assignment_status='REASSIGNED' AND old.ended_at=a.occurred_at
        WHERE JSON_VALUE(j.payload,'$.operation')='ASSIGN' AND JSON_VALUE(j.payload,'$.workflowState')='ASSIGNED'
          AND JSON_VALUE(j.payload,'$.notificationKind')='TASK_ASSIGNED'
          AND ((JSON_VALUE(j.payload,'$.previousAssignmentId') IS NULL AND JSON_VALUE(j.payload,'$.previousAssigneeId') IS NULL AND p.description=N'Task assigned.')
            OR (old.assignment_id IS NOT NULL AND LEN(LTRIM(RTRIM(JSON_VALUE(j.payload,'$.reason'))))>0
              AND p.description=CONCAT(N'Task reassigned: ',JSON_VALUE(j.payload,'$.reason'))))
          AND (JSON_VALUE(j.payload,'$.closedIntervalId') IS NULL OR EXISTS(SELECT 1 FROM dbo.work_intervals i
            WHERE i.interval_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.closedIntervalId')) AND i.work_item_kind='TASK'
              AND i.work_item_id=t.task_id AND i.user_id=old.assignee_id AND i.branch_id=t.branch_id
              AND i.closed_at=a.occurred_at AND i.closed_by=a.actor_user_id))
          AND EXISTS(SELECT 1 FROM dbo.departments d WHERE d.department_id=t.department_id AND d.branch_id=t.branch_id)
          AND (t.room_id IS NULL OR EXISTS(SELECT 1 FROM dbo.rooms r WHERE r.room_id=t.room_id AND r.branch_id=t.branch_id))
          AND (t.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas r WHERE r.area_id=t.area_id AND r.branch_id=t.branch_id))
        """);
}
