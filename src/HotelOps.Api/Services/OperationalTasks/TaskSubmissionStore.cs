using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.OperationalTasks;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskSubmission
{
    public long ProgressId { get; set; }
    public long TaskId { get; set; }
    public long BranchId { get; set; }
    public long SubmittedBy { get; set; }
    public long IntervalId { get; set; }
    public long AssignmentId { get; set; }
    public long AuditId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? ResultNote { get; set; }
    public string? HandoverNote { get; set; }
    public string CriteriaJson { get; set; } = "[]";
    public string EvidenceJson { get; set; } = "[]";
    public string RecipientsJson { get; set; } = "[]";
}

internal static class TaskSubmissionStore
{
    internal const string NotificationKind = "TASK_SUBMITTED";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await ProgressSchema.InstalledAsync(db, ct) && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskSubmission.V1' AND CONVERT(nvarchar(20),value)='1')
              AND ISNULL(COL_LENGTH('dbo.task_submission_events','result_note'),-1)=8000
              AND ISNULL(COL_LENGTH('dbo.task_submission_events','handover_note'),-1)=8000
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.task_submission_events') AND is_disabled=0 AND is_not_trusted=0)=6
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_task_submission_immutable_v1') AND is_disabled=0)
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static IQueryable<StoredTaskSubmission> Query(HotelOpsDbContext db) =>
        db.Database.SqlQuery<StoredTaskSubmission>($"""
            SELECT e.progress_id AS ProgressId,e.task_id AS TaskId,t.branch_id AS BranchId,e.submitted_by AS SubmittedBy,
              e.interval_id AS IntervalId,e.assignment_id AS AssignmentId,e.audit_id AS AuditId,e.submitted_at AS SubmittedAt,
              e.result_note AS ResultNote,e.handover_note AS HandoverNote,e.criteria_json AS CriteriaJson,
              e.evidence_json AS EvidenceJson,e.recipients_json AS RecipientsJson
            FROM dbo.task_submission_events e JOIN dbo.operational_tasks t ON t.task_id=e.task_id
            JOIN dbo.operational_task_progress p ON p.progress_id=e.progress_id AND p.task_id=e.task_id AND p.reported_by=e.submitted_by
            JOIN dbo.work_intervals i ON i.interval_id=e.interval_id AND i.work_item_kind='TASK' AND i.work_item_id=e.task_id
              AND i.user_id=e.submitted_by AND i.branch_id=t.branch_id AND i.closed_at=e.submitted_at
            JOIN dbo.operational_task_assignments x ON x.assignment_id=e.assignment_id AND x.task_id=e.task_id AND x.assignee_id=e.submitted_by
            JOIN dbo.audit_logs a ON a.audit_id=e.audit_id AND a.actor_user_id=e.submitted_by AND a.branch_id=t.branch_id
              AND a.entity_type='operational_tasks' AND a.entity_id=CONVERT(nvarchar(50),e.task_id) AND a.action='UPDATE'
              AND a.occurred_at=e.submitted_at
              AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.operation')='SUBMIT'
              AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.progressId'))=e.progress_id
            WHERE t.department_id IN(SELECT department_id FROM dbo.departments WHERE branch_id=t.branch_id)
              AND (t.room_id IS NULL OR EXISTS(SELECT 1 FROM dbo.rooms r WHERE r.room_id=t.room_id AND r.branch_id=t.branch_id))
              AND (t.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas r WHERE r.area_id=t.area_id AND r.branch_id=t.branch_id))
            """);

    internal static TaskSubmissionDto Dto(StoredTaskSubmission row)
    {
        var criteria = JsonSerializer.Deserialize<ProgressItemDto[]>(row.CriteriaJson, Json) ?? throw new InvalidOperationException("Invalid submission criteria");
        var files = JsonSerializer.Deserialize<HistoryEvidenceDto[]>(row.EvidenceJson, Json) ?? throw new InvalidOperationException("Invalid submission evidence");
        return new(row.ProgressId,row.TaskId,row.BranchId,row.SubmittedBy,
            new DateTimeOffset(DateTime.SpecifyKind(row.SubmittedAt,DateTimeKind.Utc)),row.ResultNote,row.HandoverNote,criteria,
            files.Select(f => f with { DownloadPath = $"/api/branches/{row.BranchId}/operational-tasks/{row.TaskId}/submission/evidence/{f.Id}" }).ToArray());
    }
}
