using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskDecline
{
    public long AuditId { get; set; }
    public long TaskId { get; set; }
    public long BranchId { get; set; }
    public long DeclinedBy { get; set; }
    public DateTime DeclinedAt { get; set; }
    public string RecipientsJson { get; set; } = "[]";
}

// The committed audit is the durable intent. Its assignment/progress evidence is
// verified again on delivery and source opening, never trusted from a notification.
internal static class TaskDeclineStore
{
    internal const string NotificationKind = "TASK_DECLINED";
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await ProgressSchema.InstalledAsync(db, ct) && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.TaskDecline.V1' AND CONVERT(nvarchar(20),value)='1')
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.task_progress_versions')
                AND name='CK_task_progress_state_entry_v1' AND is_disabled=0 AND is_not_trusted=0
                AND definition LIKE '%PENDING_ASSIGNMENT%') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static IQueryable<StoredTaskDecline> Query(HotelOpsDbContext db) => db.Database.SqlQuery<StoredTaskDecline>($"""
        SELECT a.audit_id AS AuditId,t.task_id AS TaskId,t.branch_id AS BranchId,a.actor_user_id AS DeclinedBy,
          a.occurred_at AS DeclinedAt,JSON_QUERY(j.payload,'$.recipients') AS RecipientsJson
        FROM dbo.audit_logs a
        CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
        JOIN dbo.operational_tasks t ON a.entity_type='operational_tasks' AND a.entity_id=CONVERT(nvarchar(50),t.task_id)
          AND a.branch_id=t.branch_id AND a.action='UPDATE'
        JOIN dbo.operational_task_assignments x ON x.assignment_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assignmentId'))
          AND x.task_id=t.task_id AND x.assignee_id=a.actor_user_id AND x.assignment_status='CANCELLED' AND x.ended_at=a.occurred_at
        JOIN dbo.operational_task_progress p ON p.progress_id=TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.progressId'))
          AND p.task_id=t.task_id AND p.reported_by=a.actor_user_id AND p.reported_at=a.occurred_at
          AND p.old_status='TODO' AND p.new_status='TODO' AND p.description=CONCAT(N'Task declined: ',JSON_VALUE(j.payload,'$.reason'))
        WHERE JSON_VALUE(j.payload,'$.operation')='DECLINE' AND JSON_VALUE(j.payload,'$.workflowState')='PENDING_ASSIGNMENT'
          AND LEN(LTRIM(RTRIM(JSON_VALUE(j.payload,'$.reason'))))>0 AND JSON_QUERY(j.payload,'$.recipients') IS NOT NULL
          AND EXISTS(SELECT 1 FROM dbo.departments d WHERE d.department_id=t.department_id AND d.branch_id=t.branch_id)
          AND (t.room_id IS NULL OR EXISTS(SELECT 1 FROM dbo.rooms r WHERE r.room_id=t.room_id AND r.branch_id=t.branch_id))
          AND (t.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas r WHERE r.area_id=t.area_id AND r.branch_id=t.branch_id))
        """);
}
