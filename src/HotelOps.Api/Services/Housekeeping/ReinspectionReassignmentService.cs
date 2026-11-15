using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private async Task<RoomTypeBatchResult> ReinspectionReassignmentOptions(long branch,long id,string? root,CancellationToken ct)
    {
        if(!await ReinspectionWorkflowInstalled(ct))return new(RoomTypeBatchStatus.SchemaRequired);
        var inspector=await db.RoomInspections.AsNoTracking().Where(i=>i.InspectionId==id&&i.Room.BranchId==branch)
            .Select(i=>(long?)i.InspectorUserId).SingleOrDefaultAsync(ct);
        if(inspector==null)return new(RoomTypeBatchStatus.NotFound);
        var loaded=await ReadReinspectionInCallerTransactionAsync(inspector.Value,branch,id,root,null,ct);
        if(loaded.Value is not ReinspectionExecutionDto source || loaded.Status!=RoomTypeBatchStatus.Success)return loaded;
        var cycle=await db.Database.SqlQuery<byte[]>($"SELECT row_version AS Value FROM dbo.room_cleaning_cycle_state WHERE room_id={source.RoomId} AND cleaning_task_id={source.CleaningTaskId}").SingleAsync(ct);
        var enabled=await CheckReinspectionWriter("FE05.UC041.REINSPECTION_REASSIGN","dbo.fe05_reassign_reinspection",true,ct)
            &&(await permissions.EvaluateBranchAsync("INSPECTION_PERFORM",branch,ct)).IsGranted;
        var choices=enabled?await EligibleInspectors(branch,clock.GetUtcNow().UtcDateTime)
            .Where(u=>u.UserId!=inspector && !db.CleaningAssignments.Any(a=>a.CleaningTaskId==source.CleaningTaskId&&a.AssigneeUserId==u.UserId&&a.AssignmentStatus!="CANCELLED")
                &&!db.CleaningProgressLogs.Any(p=>p.CleaningTaskId==source.CleaningTaskId&&p.LoggedBy==u.UserId)
                &&!db.CleaningReworks.Any(w=>w.CleaningTaskId==source.CleaningTaskId&&w.AssigneeUserId==u.UserId))
            .OrderBy(u=>u.FullName).ThenBy(u=>u.UserId).Select(u=>new ReinspectionAssigneeDto(u.UserId,u.FullName)).ToArrayAsync(ct):[];
        return new(RoomTypeBatchStatus.Success,new ReinspectionReassignmentOptions(id,branch,source.CleaningTaskId,source.RoomId,source.VersionId,inspector.Value,
            source.State,(await InspectionState(id,ct)).DueAt,source.SourceETag,WorkflowTag(cycle),choices,enabled&&choices.Length>0,
            source.ReworkId,source.ReworkETag,source.BindingETag),source.SourceETag);
    }
    public Task<RoomTypeBatchResult> ReadReinspectionReassignmentAsync(SessionReference ticket,long sid,long branch,long id,string? root,CancellationToken ct)
    {
        if(branch<=0||id<=0)throw new ValidationException("Exact reinspection scope required.");
        return BatchAsync(ticket,sid,branch,(_,token)=>ReinspectionReassignmentOptions(branch,id,root,token),ct,
            requireRoomTypeSchema:false,permissionCode:"INSPECTION_VIEW",eligibility:token=>EligibleInspector(ticket.UserId,branch,token));
    }
    public Task<RoomTypeBatchResult> ReassignReinspectionAsync(SessionReference ticket,long sid,long branch,long id,string? tag,string? key,
        ReinspectionReassignmentRequest input,string? root,CancellationToken ct)
    {
        if(branch<=0||id<=0||input==null||input.InspectorId<=0||!WorkflowStrongTag(tag)||!WorkflowStrongTag(input.CycleETag)
            ||!WorkflowStrongTag(input.ReworkETag)||!WorkflowStrongTag(input.BindingETag))throw new ValidationException("Exact source revisions required.");
        var operation=CleaningLifecycleRules.Operation(key!);var reason=WorkflowReason(input.Reason);
        var fingerprint=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{contract=ReinspectionReassignmentSource.Contract,actor=ticket.UserId,branch,id,tag,
            input.InspectorId,input.CycleETag,input.ReworkETag,input.BindingETag,reason},Json));
        return BatchAsync(ticket,sid,branch,async(actor,token)=>
        {
            if(!await ReinspectionWorkflowInstalled(token)||!await CheckReinspectionWriter("FE05.UC041.REINSPECTION_REASSIGN","dbo.fe05_reassign_reinspection",true,token))
                return new(RoomTypeBatchStatus.SchemaRequired);
            var prior=await db.Database.SqlQuery<SubmissionLedgerRow>($"SELECT result_json AS Json,fingerprint AS Fingerprint,operation_key AS OperationId FROM dbo.notification_operation_reservations WITH(HOLDLOCK) WHERE actor_user_id={actor} AND operation_key={operation} AND completed=1 AND fingerprint_version=1").SingleOrDefaultAsync(token);
            if(prior!=null)
            {
                var audits=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="REINSPECTION_ASSIGNMENT"&&a.Action=="UPDATE"&&a.ActorUserId==actor&&a.BranchId==branch&&a.NewValues==prior.Json).Take(2).ToArrayAsync(token);
                var proof=audits.Length==1?await ReinspectionReassignmentSource.VerifyAsync(db,audits[0],token):null;
                return proof!=null&&proof.InspectionId==id&&proof.OperationId==operation&&CryptographicOperations.FixedTimeEquals(prior.Fingerprint,fingerprint)
                    ?new(RoomTypeBatchStatus.Success,proof.Entity,proof.Entity.SourceETag):new(RoomTypeBatchStatus.Duplicate);
            }
            if(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_operation_reservations WITH(HOLDLOCK) WHERE actor_user_id={actor} AND operation_key={operation}").SingleAsync(token)!=0)return new(RoomTypeBatchStatus.Duplicate);
            var loaded=await ReinspectionReassignmentOptions(branch,id,root,token);
            if(loaded.Status!=RoomTypeBatchStatus.Success||loaded.Value is not ReinspectionReassignmentOptions source)return loaded;
            if(source.SourceETag!=tag||source.CycleETag!=input.CycleETag||source.ReworkETag!=input.ReworkETag||source.BindingETag!=input.BindingETag)return new(RoomTypeBatchStatus.Stale);
            if(source.InspectorId==input.InspectorId)return new(RoomTypeBatchStatus.Duplicate);
            if(await InspectorWorkedOnCycle(input.InspectorId,source.TaskId,token))return LifecycleFailure(RoomTypeBatchStatus.InvalidReference,"INSPECTOR_CLEANER_CONFLICT");
            if(!source.CanReassign||!source.Inspectors.Any(p=>p.Id==input.InspectorId))return new(RoomTypeBatchStatus.InvalidReference);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fe05_reassign_reinspection @actor={actor},@branch={branch},@inspection={id},@inspector={input.InspectorId},@revision={Convert.FromHexString(tag!.Trim('"'))},@cycleRevision={Convert.FromHexString(input.CycleETag.Trim('"'))},@reworkRevision={Convert.FromHexString(input.ReworkETag.Trim('"'))},@bindingRevision={Convert.FromHexString(input.BindingETag.Trim('"'))},@reason={reason},@operation={operation},@fingerprint={fingerprint}",token);
            var result=await ReadWrittenReinspectionResult<ReinspectionReassignmentResult>(actor,operation,token);
            if(!ReinspectionReassignmentSource.ValidResult(result)||result.Id!=id||result.BranchId!=branch||result.RoomId!=source.RoomId||result.TaskId!=source.TaskId
                ||result.VersionId!=source.VersionId||result.ReworkId!=source.ReworkId||result.PreviousInspectorId!=source.InspectorId||result.InspectorId!=input.InspectorId
                ||result.OperationId!=operation||result.ReworkETag!=source.ReworkETag||result.BindingETag!=source.BindingETag)throw new InvalidOperationException("REINSPECTION_REASSIGNMENT_RECEIPT_INVALID");
            return new(RoomTypeBatchStatus.Success,result,result.SourceETag);
        },ct,requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM",additionalUsers:[input.InspectorId],preserveCommitErrors:true,
            eligibility:async token=>await EligibleInspector(ticket.UserId,branch,token)&&await EligibleInspector(input.InspectorId,branch,token));
    }
}
