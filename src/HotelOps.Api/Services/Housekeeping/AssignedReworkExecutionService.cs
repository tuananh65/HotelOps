using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.WorkTime;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private const string AssignedReworkExecutionContract = "FE05_COMMITTED_V1";
    private sealed record AssignedReworkReceipt(string Fingerprint, Guid AuthorityOperationId,
        string AuthoritySourceDigest, ReworkCommandResult Entity);

    private async Task<bool> AssignedReworkExecutionInstalled(CancellationToken ct)
        => await db.Database.SqlQueryRaw<int>("""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.ReworkExecution.V1' AND CONVERT(nvarchar(128),value)='ACCEPT_START_V1')
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.cleaning_reworks')
                AND name='CK_rework_workflow_v1' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%ACCEPTED%')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;

    // UC035 uses committed FE05 authority. Accept/Start have no notification publication side effect.
    public async Task<RoomTypeBatchResult> ExecuteAssignedReworkAsync(SessionReference ticket,long sid,long branch,long id,
        string action,string? tag,string? key,ReworkActionRequest input,string? root,CancellationToken ct)
    {
        if(branch<=0||id<=0||action is not ("accept" or "start")||!WorkflowStrongTag(tag)||input==null||input.Remediations?.Count>0)
            throw new ValidationException("A supported action, strong ETag and rework identity are required.");
        var note=CleaningLifecycleRules.Reason(input.Note,1);
        var operation=CleaningLifecycleRules.Operation(key!);
        var fingerprint=Convert.ToHexString(ReworkFingerprint(ticket.UserId,branch,id,action,tag!,input));
        var time=new SqlWorkTimeTransactionParticipant(db);
        ReworkAuthorityProof? authority=null;
        try
        {
            return await BatchAsync(ticket,sid,branch,async(actor,token)=>
            {
                if(!await HotelOps.Api.Data.HousekeepingSchema.InstalledAsync(db,token)||!await AssignedReworkExecutionInstalled(token))
                    return new(RoomTypeBatchStatus.SchemaRequired);
                var row=await db.CleaningReworks.AsNoTracking().Where(w=>w.ReworkId==id&&w.AssigneeUserId==actor
                    &&w.CleaningTask.Room.BranchId==branch&&w.CleaningTask.Room.IsActive&&w.CleaningTask.Room.Floor.IsActive
                    &&(w.CleaningTask.Room.AreaId==null||w.CleaningTask.Room.Area!.IsActive))
                    .Select(w=>new{w.InspectionId,w.CleaningTaskId,w.CleaningTask.RoomId,w.Inspection.InspectorUserId,w.CompletedAt}).SingleOrDefaultAsync(token);
                if(row==null)return new(RoomTypeBatchStatus.NotFound);
                if(row.InspectorUserId==actor)return new(RoomTypeBatchStatus.Forbidden);
                authority=await ReadTrustedReworkAuthorityAsync(actor,branch,id,root,token);
                var inspection=await InspectionState(row.InspectionId,token);
                if(TrustedReworkExecutionConflict(authority,id,row.InspectionId,row.CleaningTaskId,row.RoomId,branch,inspection.Revision) is {} invalid)
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable,invalid);
                if(await db.CleaningReworks.CountAsync(w=>w.InspectionId==row.InspectionId&&w.Status!="COMPLETED"&&w.Status!="CANCELLED",token)!=1)
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive,"DUPLICATE_REWORK_SOURCE");
                var defects=await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={id}").ToArrayAsync(token);
                if(defects.Length is <1 or >200
                    ||await db.InspectionChecklistItems.AnyAsync(i=>i.VersionId==authority!.Binding.VersionId&&i.IsRequired&&i.IsActive
                        &&!db.InspectionResults.Any(r=>r.InspectionId==row.InspectionId&&r.ChecklistItemId==i.ItemId&&(r.Outcome=="PASS"||r.Outcome=="FAIL")),token)
                    ||await db.InspectionDefects.CountAsync(d=>defects.Contains(d.DefectId)&&d.InspectionId==row.InspectionId
                        &&d.ChecklistItem!=null&&d.ChecklistItem.VersionId==authority!.Binding.VersionId
                        &&db.InspectionResults.Any(r=>r.InspectionId==row.InspectionId&&r.ChecklistItemId==d.ChecklistItemId&&r.Outcome=="FAIL"),token)!=defects.Length)
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_DEFECT_SOURCE_INVALID");
                var prior=await db.Database.SqlQuery<string>($"""
                    SELECT new_values AS Value FROM dbo.audit_logs WITH(HOLDLOCK)
                    WHERE actor_user_id={actor} AND entity_type='cleaning_reworks'
                      AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.operationId'))={operation}
                    """).ToArrayAsync(token);
                if(prior.Length>0)
                {
                    if(prior.Length!=1)return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_DURABLE_RESULT_INVALID");
                    AssignedReworkReceipt? stored;
                    try{stored=JsonSerializer.Deserialize<AssignedReworkReceipt>(prior[0],Json);}
                    catch(JsonException){stored=null;}
                    if(stored?.Fingerprint!=fingerprint||stored.AuthorityOperationId!=authority!.OperationId
                        ||stored.AuthoritySourceDigest!=Convert.ToHexString(authority.SourceDigest)
                        ||!ValidReworkReceipt(stored.Entity,actor,branch,id,row.CleaningTaskId,row.InspectionId,operation,action,AssignedReworkExecutionContract))
                        return LifecycleFailure(RoomTypeBatchStatus.Duplicate,"REWORK_OPERATION_CONFLICT");
                    return new(RoomTypeBatchStatus.Success,stored.Entity,stored.Entity.SourceETag);
                }
                var state=await ReworkState(id,token);
                if(tag!=WorkflowTag(state.Revision))return new(RoomTypeBatchStatus.Stale);
                var next=ReworkLifecycleRules.Next(state.State,action);
                if(next==null||row.CompletedAt!=null)return new(RoomTypeBatchStatus.Inactive);
                var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(token),DateTimeKind.Utc);
                var legacy=action=="accept"?"PENDING":"IN_PROGRESS";
                if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET workflow_status={next},status={legacy},note={note} WHERE rework_id={id} AND row_version={state.Revision}",token)!=1)
                    return new(RoomTypeBatchStatus.Stale);
                long? interval=null;
                if(action=="start")
                {
                    var opened=await time.OpenIntervalAsync(new(actor,actor,branch,"REWORK",id),token);
                    interval=opened.IntervalId;now=opened.OpenedAtUtc;db.ChangeTracker.Clear();
                }
                var readiness=await new HotelOps.Api.Services.Rooms.EffectiveRoomReadiness(db).ReadAsync([row.RoomId],now,token);
                if(!readiness.TryGetValue(row.RoomId,out var roomState)||roomState.Readiness is not ("BLOCKED" or "REWORK_REQUIRED"))
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_READINESS_UNPROVEN");
                var result=new ReworkCommandResult(id,branch,row.CleaningTaskId,row.InspectionId,actor,action,next,operation,
                    WorkflowTag((await ReworkState(id,token)).Revision),now,interval,null,roomState.Readiness,"NOT_REQUIRED",AssignedReworkExecutionContract);
                await BatchAuditAsync(actor,branch,"cleaning_reworks",id,new{state.State},new{operationId=operation,fingerprint,
                    authorityOperationId=authority!.OperationId,authoritySourceDigest=Convert.ToHexString(authority.SourceDigest),entity=result},
                    ["workflowStatus","workInterval"],token);
                return new(RoomTypeBatchStatus.Success,result,result.SourceETag);
            },ct,requireRoomTypeSchema:false,
                featurePermission:token=>ReworkAccess(ticket.UserId,branch,id,"CLEANING_EXECUTE",token),
                eligibility:token=>DueHousekeeper(ticket.UserId,branch,DateTime.MaxValue,token),
                afterUserLocks:token=>time.LockUsersAsync([ticket.UserId],token),
                beforeCommit:async token=>
                {
                    if(authority==null||!SameTrustedReworkAuthority(authority,await ReadTrustedReworkAuthorityAsync(ticket.UserId,branch,id,root,token)))
                        throw new InvalidOperationException("REWORK_AUTHORITY_CHANGED_BEFORE_COMMIT");
                });
        }
        catch(WorkTimeContractException error){return LifecycleTimeFailure(error);}
    }
}
