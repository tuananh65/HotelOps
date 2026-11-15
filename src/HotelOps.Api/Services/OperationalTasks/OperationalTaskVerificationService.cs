using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<long[]> TaskCyclePerformers(long task,long submission,CancellationToken ct)
    {
        // Return is further work in this processing cycle, not UC106 Reopen.
        // No verified reopen boundary exists yet: retain every recorded performer.
        return await db.WorkIntervals.AsNoTracking().Where(i=>i.WorkItemKind=="TASK"&&i.WorkItemId==task
            ).Select(i=>i.UserId).Distinct().Order().ToArrayAsync(ct);
    }
    private async Task<long> TaskEffort(long id,long branch,CancellationToken ct)=>await db.Database.SqlQuery<long>($"""
        SELECT COALESCE(SUM(DATEDIFF_BIG(second,effective_started_at,effective_ended_at)),0) AS Value
        FROM dbo.work_intervals WHERE work_item_kind='TASK' AND work_item_id={id} AND branch_id={branch}
          AND closed_at IS NOT NULL AND effective_ended_at IS NOT NULL
        """).SingleAsync(ct);

    private async Task<PermissionEvaluation> VerificationManager(long actor,long branch,CancellationToken ct)=>
        new((await TaskSubmissionAccess.ManagersAsync(db,branch,clock.GetUtcNow().UtcDateTime,ct)).Contains(actor)?PermissionDecision.Granted:PermissionDecision.Denied);

    public Task<RoomTypeBatchResult> ReadTaskVerificationAsync(SessionReference ticket,long sid,long branch,long id,CancellationToken ct)=>
        BatchAsync(ticket,sid,branch,async(actor,token)=>
        {
            if(!await TaskVerificationStore.InstalledAsync(db,token))return new(RoomTypeBatchStatus.SchemaRequired);
            var submission=await TaskSubmissionStore.Query(db).Where(s=>s.TaskId==id&&s.BranchId==branch).OrderByDescending(s=>s.ProgressId).FirstOrDefaultAsync(token);
            if(submission==null)return new(RoomTypeBatchStatus.NotFound);
            var task=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskId==id,token);
            var version=await ProgressRevision(false,id,token);
            if(version==null)return new(RoomTypeBatchStatus.SchemaRequired);
            var decisions=await TaskVerificationStore.Query(db).Where(v=>v.TaskId==id).OrderBy(v=>v.SubmissionId).ToArrayAsync(token);
            var performers=await TaskCyclePerformers(id,submission.ProgressId,token);
            var separate=actor!=task.CreatedBy&&actor!=submission.SubmittedBy&&!performers.Contains(actor);
            var supported=task.SourceId==null&&task.SourceType is null or "MANUAL";
            var can=version.State=="PENDING_VERIFICATION"&&separate&&supported&&(await VerificationManager(actor,branch,token)).IsGranted;
            return new(RoomTypeBatchStatus.Success,new TaskVerificationReviewDto(id,branch,submission.ProgressId,version.State,
                await TaskEffort(id,branch,token),can,!separate?"MSG22":!supported?"SOURCE_PROVENANCE_REQUIRED":null,decisions.Select(TaskVerificationStore.Dto).ToArray()),WorkflowTag(version.Revision));
        },ct,requireRoomTypeSchema:false,featurePermission:async token=>
            (await VerificationManager(ticket.UserId,branch,token)).IsGranted?new(PermissionDecision.Granted):
                new(await TaskVerificationStore.ExecutorReadAsync(db,ticket.UserId,branch,id,clock.GetUtcNow().UtcDateTime,token)?PermissionDecision.Granted:PermissionDecision.Denied));

    public async Task<RoomTypeBatchResult> VerifyOperationalTaskAsync(SessionReference ticket,long sid,long branch,long id,string expected,VerifyOperationalTaskRequest input,CancellationToken ct)
    {
        if(id<=0||input.SubmissionId<=0||!WorkflowStrongTag(expected)||input.Decision is not ("ACCEPT" or "RETURN"))throw new ValidationException("Valid task, submission, decision and strong ETag required.");
        var note=TaskResultText(input.Note);
        if(note==null||input.Decision=="RETURN"&&note.Length<10)throw new ValidationException("Verification note is required; Return reason must contain at least 10 characters.");
        long? owner=null;
        if(await TaskSubmissionStore.InstalledAsync(db,ct))owner=await TaskSubmissionStore.Query(db).Where(s=>s.ProgressId==input.SubmissionId&&s.TaskId==id&&s.BranchId==branch).Select(s=>(long?)s.SubmittedBy).SingleOrDefaultAsync(ct);
        var time=new SqlWorkTimeTransactionParticipant(db);
        return await BatchAsync(ticket,sid,branch,async(actor,token)=>
        {
            if(!await TaskVerificationStore.InstalledAsync(db,token))return new(RoomTypeBatchStatus.SchemaRequired);
            var task=await db.OperationalTasks.FromSqlInterpolated($"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}").AsNoTracking().SingleOrDefaultAsync(token);
            if(task==null||task.BranchId!=branch)return new(RoomTypeBatchStatus.NotFound);
            var version=await ProgressRevision(false,id,token);
            if(version==null)return new(RoomTypeBatchStatus.SchemaRequired);
            if(WorkflowTag(version.Revision)!=expected)return new(RoomTypeBatchStatus.Stale);
            if(version.State!="PENDING_VERIFICATION"||task.Status!="IN_PROGRESS"||task.CompletedAt!=null)return new(RoomTypeBatchStatus.Inactive);
            var submitted=await TaskSubmissionStore.Query(db).Where(s=>s.TaskId==id&&s.BranchId==branch).OrderByDescending(s=>s.ProgressId).FirstOrDefaultAsync(token);
            if(submitted==null||submitted.ProgressId!=input.SubmissionId||owner!=submitted.SubmittedBy)return new(RoomTypeBatchStatus.Stale);
            if(task.SourceId!=null||task.SourceType is not (null or "MANUAL"))return new(RoomTypeBatchStatus.SchemaRequired); // Linked requester provenance must be resolved, never guessed.
            var performers=await TaskCyclePerformers(id,submitted.ProgressId,token);
            if(actor==task.CreatedBy||actor==submitted.SubmittedBy||performers.Contains(actor))return new(RoomTypeBatchStatus.Forbidden,new TaskEntryFailure("MSG22"));
            if(!performers.Contains(submitted.SubmittedBy)||!await ProgressOwned(false,id,branch,submitted.SubmittedBy,token)
                ||!await db.OperationalTaskAssignments.AnyAsync(a=>a.AssignmentId==submitted.AssignmentId&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null,token)
                ||await db.WorkIntervals.AnyAsync(i=>i.WorkItemKind=="TASK"&&i.WorkItemId==id&&i.ClosedAt==null,token)
                ||!await db.Departments.AnyAsync(d=>d.DepartmentId==task.DepartmentId&&d.BranchId==branch&&d.IsActive,token))return new(RoomTypeBatchStatus.Inactive);
            var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token);
            var accept=input.Decision=="ACCEPT";var state=accept?"CLOSED":"ACCEPTED";var physical=accept?"COMPLETED":"TODO";
            var effort=await TaskEffort(id,branch,token);if(effort<0||now<submitted.SubmittedAt)return new(RoomTypeBatchStatus.Inactive);
            var audit=new AuditLog{ActorUserId=actor,BranchId=branch,EntityType="operational_tasks",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
                OldValues=JsonSerializer.Serialize(new{workflowState="PENDING_VERIFICATION"}),
                NewValues=JsonSerializer.Serialize(new{operation="VERIFY",submissionId=submitted.ProgressId,decision=input.Decision,workflowState=state,requestedBy=task.CreatedBy,submittedBy=submitted.SubmittedBy,performedBy=performers,verifiedBy=actor,note,actualEffortSeconds=effort,notificationKind=accept?"TASK_VERIFIED":"TASK_RETURNED"}),
                ChangedFields="[\"workflowState\",\"verification\",\"assignment\",\"notificationIntent\"]"};
            db.AuditLogs.Add(audit);await db.SaveChangesAsync(token);db.ChangeTracker.Clear();
            var performedJson=JsonSerializer.Serialize(performers);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_verification_events(submission_id,requested_by,performed_by_json,verified_by,decision,note,decided_at,actual_effort_seconds,audit_id) VALUES({submitted.ProgressId},{task.CreatedBy},{performedJson},{actor},{input.Decision},{note},{now},{effort},{audit.AuditId})",token);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status={state} WHERE task_id={id}; UPDATE dbo.operational_tasks SET status={physical},completed_at={ (accept?(DateTime?)now:null)},updated_at={now} WHERE task_id={id}",token);
            if(accept)await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.operational_task_assignments SET assignment_status='COMPLETED',ended_at={now} WHERE assignment_id={submitted.AssignmentId}",token);
            db.OperationalTaskProgresses.Add(new(){TaskId=id,ReportedBy=actor,ReportedAt=now,ProgressPercent=task.ProgressPercent,OldStatus="IN_PROGRESS",NewStatus=physical,Description=accept?"Task verification accepted.":"Task returned for more work."});
            await db.SaveChangesAsync(token);db.ChangeTracker.Clear();
            return new(RoomTypeBatchStatus.Success,new{taskId=id,branchId=branch,submissionId=submitted.ProgressId,state,messageCode=accept?"MSG08":"MSG20"},WorkflowTag((await ProgressRevision(false,id,token))!.Revision));
        },ct,additionalUsers:owner is {} user?[user]:[],requireRoomTypeSchema:false,featurePermission:token=>VerificationManager(ticket.UserId,branch,token),
            afterUserLocks:owner is {} target?token=>time.LockUsersAsync([ticket.UserId,target],token):null);
    }
}
