using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

internal sealed class StoredTaskVerification
{
    public long SubmissionId {get;set;}
    public long TaskId {get;set;}
    public long BranchId {get;set;}
    public long AssignmentId {get;set;}
    public long RequestedBy {get;set;}
    public long SubmittedBy {get;set;}
    public long VerifiedBy {get;set;}
    public long AuditId {get;set;}
    public string PerformedByJson {get;set;}="[]";
    public string Decision {get;set;}="";
    public string Note {get;set;}="";
    public DateTime DecidedAt {get;set;}
    public long ActualEffortSeconds {get;set;}
}

internal static class TaskVerificationStore
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>
        await TaskSubmissionStore.InstalledAsync(db,ct) && await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.TaskVerification.V1' AND CONVERT(nvarchar(20),value)='1')
          AND ISNULL(COL_LENGTH('dbo.task_verification_events','note'),-1)=8000
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.task_verification_events') AND is_disabled=0 AND is_not_trusted=0)=4
          AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_task_verification_immutable_v1') AND is_disabled=0)
        THEN 1 ELSE 0 END AS Value
        """).SingleAsync(ct)==1;

    internal static IQueryable<StoredTaskVerification> Query(HotelOpsDbContext db)=>db.Database.SqlQuery<StoredTaskVerification>($"""
        SELECT v.submission_id AS SubmissionId,e.task_id AS TaskId,t.branch_id AS BranchId,e.assignment_id AS AssignmentId,
          v.requested_by AS RequestedBy,e.submitted_by AS SubmittedBy,v.verified_by AS VerifiedBy,v.audit_id AS AuditId,
          v.performed_by_json AS PerformedByJson,v.decision AS Decision,v.note AS Note,v.decided_at AS DecidedAt,v.actual_effort_seconds AS ActualEffortSeconds
        FROM dbo.task_verification_events v JOIN dbo.task_submission_events e ON e.progress_id=v.submission_id
        JOIN dbo.operational_tasks t ON t.task_id=e.task_id
        JOIN dbo.operational_task_assignments x ON x.assignment_id=e.assignment_id AND x.task_id=e.task_id AND x.assignee_id=e.submitted_by
        JOIN dbo.audit_logs a ON a.audit_id=v.audit_id AND a.actor_user_id=v.verified_by AND a.branch_id=t.branch_id
          AND a.entity_type='operational_tasks' AND a.entity_id=CONVERT(nvarchar(50),e.task_id) AND a.action='UPDATE' AND a.occurred_at=v.decided_at
          AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.operation')='VERIFY'
          AND TRY_CONVERT(bigint,JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.submissionId'))=v.submission_id
          AND JSON_VALUE(CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'[]' END,'$.decision')=v.decision
        WHERE v.verified_by<>v.requested_by AND v.verified_by<>e.submitted_by
          AND NOT EXISTS(SELECT 1 FROM OPENJSON(v.performed_by_json) p WHERE TRY_CONVERT(bigint,p.value)=v.verified_by)
          AND t.department_id IN(SELECT department_id FROM dbo.departments WHERE branch_id=t.branch_id)
          AND (t.room_id IS NULL OR EXISTS(SELECT 1 FROM dbo.rooms r WHERE r.room_id=t.room_id AND r.branch_id=t.branch_id))
          AND (t.area_id IS NULL OR EXISTS(SELECT 1 FROM dbo.areas r WHERE r.area_id=t.area_id AND r.branch_id=t.branch_id))
        """);

    internal static IQueryable<long> ClosedTaskIds(HotelOpsDbContext db,long user)=>
        Query(db).Where(v=>v.SubmittedBy==user&&v.Decision=="ACCEPT"
            && db.OperationalTasks.Any(t=>t.TaskId==v.TaskId&&t.Status=="COMPLETED"&&t.CompletedAt==v.DecidedAt)
            && db.OperationalTaskAssignments.Any(a=>a.AssignmentId==v.AssignmentId&&a.AssignmentStatus=="COMPLETED"&&a.EndedAt==v.DecidedAt))
            .Select(v=>v.TaskId);

    internal static async Task<bool> ExecutorReadAsync(HotelOpsDbContext db,long user,long branch,long task,DateTime now,CancellationToken ct)
    {
        var data=new EfPermissionDataSource(db);
        if(!await data.IsActiveUserAsync(user,ct)||!await data.IsActiveBranchAsync(branch,ct)
            ||!await db.UserBranchAccesses.AnyAsync(a=>a.UserId==user&&a.BranchId==branch&&a.GrantedAt<=now&&a.RevokedAt==null,ct))return false;
        if(!(await data.ReadPathsAsync(user,"TASK_VIEW",now,ct)).Any(p=>p.PermissionCode=="TASK_VIEW"&&PermissionGrantRules.IsEligible(p,now)
            && (PermissionGrantRules.EffectiveScope(p)=="GLOBAL"||p.BranchId==branch&&PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "OWN")))return false;
        if(!await db.OperationalTasks.AnyAsync(t=>t.TaskId==task&&t.BranchId==branch&&t.Department.BranchId==branch,ct))return false;
        var owners=await db.OperationalTaskAssignments.AsNoTracking().Where(a=>a.TaskId==task&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=now).Select(a=>a.AssigneeId).Take(2).ToArrayAsync(ct);
        return owners.Length==1&&owners[0]==user || await InstalledAsync(db,ct)&&await ClosedTaskIds(db,user).ContainsAsync(task,ct);
    }

    internal static TaskVerificationDecisionDto Dto(StoredTaskVerification v)=>new(v.SubmissionId,v.Decision,v.Note,v.RequestedBy,v.SubmittedBy,
        JsonSerializer.Deserialize<long[]>(v.PerformedByJson)??[],v.VerifiedBy,new DateTimeOffset(DateTime.SpecifyKind(v.DecidedAt,DateTimeKind.Utc)),v.ActualEffortSeconds);
}
