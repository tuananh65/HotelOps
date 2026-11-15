using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private static string? TaskResultText(string? value)
    {
        if(value?.Length>4000 || value?.Any(c=>char.IsControl(c) && c is not ('\r' or '\n' or '\t'))==true)
            throw new ValidationException("Result and handover notes must be at most 4000 characters without unsupported control characters.");
        return string.IsNullOrWhiteSpace(value)?null:value.Trim();
    }

    public async Task<RoomTypeBatchResult> SubmitOperationalTaskAsync(SessionReference ticket,long sid,long branch,long id,
        string expected,SubmitOperationalTaskRequest input,string root,CancellationToken ct)
    {
        if(id<=0 || !WorkflowStrongTag(expected) || input.Items==null || input.Items.Count>100 || input.Items.Any(i=>i==null)
            || input.Items.Select(i=>i.Id).Distinct().Count()!=input.Items.Count || (input.Photos?.Count??0)>5)
            throw new ValidationException("A task, strong ETag and valid completion results are required.");
        var resultNote=TaskResultText(input.ResultNote);var handover=TaskResultText(input.HandoverNote);
        using var evidence=new EvidenceFileLifetime();var time=new SqlWorkTimeTransactionParticipant(db);
        try
        {
            return await BatchAsync(ticket,sid,branch,async(actor,token)=>
            {
                if(!await TaskSubmissionStore.InstalledAsync(db,token))return new(RoomTypeBatchStatus.SchemaRequired);
                var task=await db.OperationalTasks.FromSqlInterpolated($"SELECT * FROM dbo.operational_tasks WITH(UPDLOCK,HOLDLOCK) WHERE task_id={id}").AsNoTracking().SingleOrDefaultAsync(token);
                if(task==null||task.BranchId!=branch)return new(RoomTypeBatchStatus.NotFound);
                if(!await ProgressOwned(false,id,branch,actor,token))return new(RoomTypeBatchStatus.Forbidden);
                var version=await ProgressRevision(false,id,token);
                if(version==null)return new(RoomTypeBatchStatus.SchemaRequired);
                if(WorkflowTag(version.Revision)!=expected)return new(RoomTypeBatchStatus.Stale);
                if(version.State!="IN_PROGRESS"||task.Status!="IN_PROGRESS"||task.ActualStartAt==null||task.CompletedAt!=null)return new(RoomTypeBatchStatus.Inactive);
                var before=await ProgressDto(false,id,branch,version.State,token);
                foreach(var item in input.Items)
                {
                    if(!before.Items.Any(i=>i.Id==item.Id)||item.Done!=null||item.Outcome is not ("PASS" or "FAIL"))throw new ValidationException("Unknown completion criterion or invalid result.");
                    var note=ChecklistText(item.Note,1000,false);
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_completion_criteria SET outcome={item.Outcome},note={note} WHERE criterion_id={item.Id} AND task_id={id}",token);
                }
                var results=await ProgressDto(false,id,branch,version.State,token);
                var missing=results.Items.Where(i=>i.Required&&i.Outcome==null).Select(i=>i.Label).ToArray();
                if(missing.Length>0)return new(RoomTypeBatchStatus.InvalidReference,new TaskSubmissionFailure("MSG03",missing));
                var interval=await db.WorkIntervals.AsNoTracking().SingleOrDefaultAsync(i=>i.UserId==actor&&i.ClosedAt==null,token);
                if(interval==null||interval.BranchId!=branch||interval.WorkItemKind!="TASK"||interval.WorkItemId!=id)return new(RoomTypeBatchStatus.Inactive);
                var closed=await time.CloseIntervalAsync(new(actor,actor,branch,"TASK",id),interval.IntervalId,interval.RowVersion,token);
                var ended=closed.ClosedAtUtc!.Value;
                if(ended<task.ActualStartAt)return new(RoomTypeBatchStatus.Inactive);
                var assignment=await db.OperationalTaskAssignments.AsNoTracking().Where(a=>a.TaskId==id&&a.AssigneeId==actor&&a.AssignmentStatus=="ACTIVE"&&a.EndedAt==null&&a.AssignedAt<=ended).SingleAsync(token);
                var progress=new OperationalTaskProgress {TaskId=id,ReportedBy=actor,ReportedAt=ended,ProgressPercent=task.ProgressPercent,
                    OldStatus=task.Status,NewStatus=task.Status,Description="Task submitted for verification."};
                db.OperationalTaskProgresses.Add(progress);await db.SaveChangesAsync(token);db.ChangeTracker.Clear();
                foreach(var photo in input.Photos??[])
                {
                    var attachment=await StoreFeatureEvidence("TASK",id,actor,photo,root,evidence.Files,token);
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.progress_evidence_links(attachment_id,task_progress_id) VALUES({attachment},{progress.ProgressId})",token);
                }
                var snapshot=await ProgressDto(false,id,branch,"PENDING_VERIFICATION",token);
                var recipients=(await TaskSubmissionAccess.ManagersAsync(db,branch,ended,token)).Where(u=>u!=actor).ToArray();
                var audit=new AuditLog {ActorUserId=actor,BranchId=branch,EntityType="operational_tasks",EntityId=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=ended,
                    OldValues=JsonSerializer.Serialize(new{workflowState="IN_PROGRESS"},TaskSubmissionStore.Json),
                    NewValues=JsonSerializer.Serialize(new{workflowState="PENDING_VERIFICATION",operation="SUBMIT",progressId=progress.ProgressId,intervalId=interval.IntervalId,assignmentId=assignment.AssignmentId,submittedBy=actor,actualEnd=ended,notificationKind=TaskSubmissionStore.NotificationKind},TaskSubmissionStore.Json),
                    ChangedFields="[\"workflowState\",\"submission\",\"workInterval\",\"notificationIntent\"]"};
                db.AuditLogs.Add(audit);await db.SaveChangesAsync(token);db.ChangeTracker.Clear();
                var criteriaJson=JsonSerializer.Serialize(snapshot.Items,TaskSubmissionStore.Json);
                var evidenceJson=JsonSerializer.Serialize(snapshot.Evidence,TaskSubmissionStore.Json);
                var recipientsJson=JsonSerializer.Serialize(recipients,TaskSubmissionStore.Json);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT dbo.task_submission_events(progress_id,task_id,submitted_by,interval_id,assignment_id,audit_id,submitted_at,result_note,handover_note,criteria_json,evidence_json,recipients_json)
                    VALUES({progress.ProgressId},{id},{actor},{interval.IntervalId},{assignment.AssignmentId},{audit.AuditId},{ended},{resultNote},{handover},{criteriaJson},{evidenceJson},{recipientsJson})
                    """,token);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.task_progress_versions SET workflow_status='PENDING_VERIFICATION' WHERE task_id={id}; UPDATE dbo.operational_tasks SET updated_at={ended} WHERE task_id={id}",token);
                // Pending Verification is not Closed. Preserve physical IN_PROGRESS and
                // completed_at; the submission/closed interval records the actual end.
                return new(RoomTypeBatchStatus.Success,snapshot,WorkflowTag((await ProgressRevision(false,id,token))!.Revision));
            },ct,requireRoomTypeSchema:false,featurePermission:token=>ProgressAccess(false,id,branch,ticket.UserId,token),
                afterUserLocks:token=>time.LockUsersAsync([ticket.UserId],token),beforeCommit:evidence.BeforeCommitAsync,preserveCommitErrors:true);
        }
        catch(EvidenceInputException){return new(RoomTypeBatchStatus.InvalidReference,new TaskSubmissionFailure("MSG23"));}
        catch(WorkTimeContractException error)
        {
            return new(error.Code switch {"TIME_SCOPE_DENIED" or "TIME_ACTOR_INVALID"=>RoomTypeBatchStatus.Forbidden,"TIME_STALE"=>RoomTypeBatchStatus.Stale,
                "TIME_WORKDAY_REQUIRED" or "TIME_STATE_CONFLICT"=>RoomTypeBatchStatus.Inactive,_=>RoomTypeBatchStatus.Unavailable});
        }
    }

    public Task<RoomTypeBatchResult> ReadTaskSubmissionAsync(SessionReference ticket,long sid,long branch,long id,string root,CancellationToken ct,long? attachment=null)
        =>BatchAsync(ticket,sid,branch,async(_,token)=>
        {
            if(!await TaskSubmissionStore.InstalledAsync(db,token))return new(RoomTypeBatchStatus.SchemaRequired);
            var row=await TaskSubmissionStore.Query(db).Where(s=>s.TaskId==id&&s.BranchId==branch).OrderByDescending(s=>s.ProgressId).FirstOrDefaultAsync(token);
            if(row==null)return new(RoomTypeBatchStatus.NotFound);
            var dto=TaskSubmissionStore.Dto(row);
            if(attachment is {} fileId)
            {
                var file=await HistoryFile(dto.Evidence,fileId,root,token);
                return file==null?new(RoomTypeBatchStatus.NotFound):new(RoomTypeBatchStatus.Success,file);
            }
            return new(RoomTypeBatchStatus.Success,dto);
        },ct,requireRoomTypeSchema:false,featurePermission:async token=>
        {
            if(await ProgressOwned(false,id,branch,ticket.UserId,token) && (await ProgressAccess(false,id,branch,ticket.UserId,token)).IsGranted)return new(PermissionDecision.Granted);
            return new((await TaskSubmissionAccess.ManagersAsync(db,branch,clock.GetUtcNow().UtcDateTime,token)).Contains(ticket.UserId)?PermissionDecision.Granted:PermissionDecision.Denied);
        });
}
