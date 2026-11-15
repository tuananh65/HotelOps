using HotelOps.Api.Data;
using HotelOps.Api.DTOs.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskReopen
{
    public long PreviousSubmissionId { get; set; }
    public long TaskId { get; set; }
    public long BranchId { get; set; }
    public long DepartmentId { get; set; }
    public long AssignmentId { get; set; }
    public long AssigneeId { get; set; }
    public long ReopenedBy { get; set; }
    public DateTime ReopenedAt { get; set; }
    public long IntervalCutoff { get; set; }
    public long AuditId { get; set; }
    public string Reason { get; set; } = "";
    public string Instructions { get; set; } = "";
}

internal static class TaskReopenStore
{
    internal const string NotificationKind = "TASK_REOPENED";
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await TaskVerificationStore.InstalledAsync(db, ct) && await TaskPlanningStore.InstalledAsync(db, ct)
        && await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskReopen.V1' AND CONVERT(nvarchar(20),value)='1')
          AND COL_LENGTH('dbo.task_reopen_events','reason')=8000 AND COL_LENGTH('dbo.task_reopen_events','instructions')=8000
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.task_reopen_events') AND is_disabled=0 AND is_not_trusted=0)=6
          AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.task_reopen_events') AND name='CK_task_reopen_v1' AND is_disabled=0 AND is_not_trusted=0)
          AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.task_reopen_events') AND is_unique=1 AND is_disabled=0
            AND name IN('PK_task_reopen_events','UQ_task_reopen_assignment','UQ_task_reopen_audit'))=3
          AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_task_reopen_immutable_v1') AND is_disabled=0)
          AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_task_progress_criteria_v1') AND is_disabled=0
            AND OBJECT_DEFINITION(object_id) LIKE '%task_reopen_events%')
        THEN 1 ELSE 0 END AS Value
        """).SingleAsync(ct) == 1;

    // Immutable event + accepted submission + completed prior assignment + audit
    // prove a new cycle. Neither a JSON claim nor a task status is sufficient.
    internal static IQueryable<StoredTaskReopen> Query(HotelOpsDbContext db) => db.Database.SqlQuery<StoredTaskReopen>($"""
        SELECT r.previous_submission_id AS PreviousSubmissionId,r.task_id AS TaskId,t.branch_id AS BranchId,t.department_id AS DepartmentId,
          r.assignment_id AS AssignmentId,x.assignee_id AS AssigneeId,r.reopened_by AS ReopenedBy,r.reopened_at AS ReopenedAt,
          r.interval_cutoff AS IntervalCutoff,r.audit_id AS AuditId,r.reason AS Reason,r.instructions AS Instructions
        FROM dbo.task_reopen_events r JOIN dbo.operational_tasks t ON t.task_id=r.task_id
        JOIN dbo.task_submission_events s ON s.progress_id=r.previous_submission_id AND s.task_id=r.task_id
        JOIN dbo.task_verification_events v ON v.submission_id=s.progress_id AND v.decision='ACCEPT' AND v.decided_at<=r.reopened_at
        JOIN dbo.operational_task_assignments old ON old.assignment_id=s.assignment_id AND old.task_id=r.task_id
          AND old.assignee_id=s.submitted_by AND old.assignment_status='COMPLETED' AND old.ended_at=v.decided_at
        JOIN dbo.operational_task_assignments x ON x.assignment_id=r.assignment_id AND x.task_id=r.task_id
          AND x.assigned_by=r.reopened_by AND x.assigned_at=r.reopened_at
        JOIN dbo.work_intervals i ON i.interval_id=r.interval_cutoff AND i.work_item_kind='TASK' AND i.work_item_id=r.task_id
          AND i.branch_id=t.branch_id AND i.closed_at<=v.decided_at
        JOIN dbo.audit_logs a ON a.audit_id=r.audit_id AND a.actor_user_id=r.reopened_by AND a.occurred_at=r.reopened_at
          AND a.branch_id=t.branch_id AND a.entity_type='operational_tasks' AND a.entity_id=CONVERT(nvarchar(50),r.task_id) AND a.action='UPDATE'
        CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END AS payload) j
        WHERE JSON_VALUE(j.payload,'$.operation')='REOPEN' AND JSON_VALUE(j.payload,'$.workflowState')='ASSIGNED'
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.previousSubmissionId'))=r.previous_submission_id
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assignmentId'))=r.assignment_id
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.assigneeId'))=x.assignee_id
          AND TRY_CONVERT(bigint,JSON_VALUE(j.payload,'$.intervalCutoff'))=r.interval_cutoff
          AND JSON_VALUE(j.payload,'$.reason')=r.reason AND JSON_VALUE(j.payload,'$.instructions')=r.instructions
          AND JSON_VALUE(j.payload,'$.notificationKind')='TASK_REOPENED'
          AND EXISTS(SELECT 1 FROM dbo.departments d WHERE d.department_id=t.department_id AND d.branch_id=t.branch_id)
          AND (t.room_id IS NULL OR EXISTS(SELECT 1 FROM dbo.rooms q WHERE q.room_id=t.room_id AND q.branch_id=t.branch_id))
          AND (t.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas q WHERE q.area_id=t.area_id AND q.branch_id=t.branch_id))
        """);

    internal static TaskReopenEventDto Dto(StoredTaskReopen r) => new(r.PreviousSubmissionId, r.AssignmentId, r.AssigneeId,
        r.ReopenedBy, new DateTimeOffset(DateTime.SpecifyKind(r.ReopenedAt, DateTimeKind.Utc)), r.Reason, r.Instructions);
}
