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
    private async Task<bool> ReworkSubmissionWriterAvailable(CancellationToken ct)
        =>await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS Value FROM dbo.fe04_rework_authority_issuers x JOIN sys.sql_modules m ON m.object_id=x.writer_object_id
            WHERE x.issuer='FE04.UC036' AND x.version=1 AND x.is_active=1 AND x.writer_object_id=OBJECT_ID('dbo.fe04_commit_rework_submission')
              AND x.source_commit LIKE REPLICATE('[0-9a-f]',40) AND m.execute_as_principal_id=-2
              AND HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition))=x.module_digest
              AND HAS_PERMS_BY_NAME('dbo.fe04_commit_rework_submission','OBJECT','EXECUTE')=1
            """).SingleAsync(ct)==1;

    // UC036 owns submission only. UC047 owns final Completed/Failed and inspection assignment.
    public async Task<RoomTypeBatchResult> SubmitAssignedReworkAsync(SessionReference ticket,long sid,long branch,long id,
        string? tag,string? key,ReworkActionRequest input,string? root,CancellationToken ct)
    {
        if(branch<=0||id<=0||input==null||!WorkflowStrongTag(tag))throw new ValidationException("A rework identity and strong ETag are required.");
        var note=CleaningLifecycleRules.Reason(input.Note,1);var operation=CleaningLifecycleRules.Operation(key!);
        ReworkLifecycleRules.ValidateRemediations(input.Remediations?.Select(r=>r?.DefectId??0).ToArray()??[],input.Remediations);
        var fingerprint=ReworkFingerprint(ticket.UserId,branch,id,"complete",tag!,input);
        var files=new List<string>();var commitAttempted=false;ReworkAuthorityProof? authority=null;byte[]? remediationDigest=null;long task=0;
        var time=new SqlWorkTimeTransactionParticipant(db);
        try
        {
            return await BatchAsync(ticket,sid,branch,async(actor,token)=>
            {
                if(!await HotelOps.Api.Data.HousekeepingSchema.InstalledAsync(db,token)||!await ReworkSubmissionInstalled(token))
                    return new(RoomTypeBatchStatus.SchemaRequired);
                var row=await db.CleaningReworks.AsNoTracking().Where(w=>w.ReworkId==id&&w.AssigneeUserId==actor
                    &&w.CleaningTask.Room.BranchId==branch&&w.CleaningTask.Room.IsActive&&w.CleaningTask.Room.Floor.IsActive
                    &&(w.CleaningTask.Room.AreaId==null||w.CleaningTask.Room.Area!.IsActive))
                    .Select(w=>new{w.CleaningTaskId,w.InspectionId,w.CleaningTask.RoomId,w.Inspection.InspectorUserId,w.CompletedAt}).SingleOrDefaultAsync(token);
                if(row==null)return new(RoomTypeBatchStatus.NotFound);
                if(row.InspectorUserId==actor)return new(RoomTypeBatchStatus.Forbidden);
                task=row.CleaningTaskId;
                var prior=await ReadReworkSubmissionReceipt(actor,branch,id,operation,fingerprint,root,token);
                if(prior.Exists)return prior.Receipt is {} receipt
                    ?new(RoomTypeBatchStatus.Success,receipt.Entity,receipt.Entity.SourceETag)
                    :LifecycleFailure(RoomTypeBatchStatus.Duplicate,"REWORK_OPERATION_CONFLICT");
                if(await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.audit_logs WHERE actor_user_id={actor}
                      AND TRY_CONVERT(uniqueidentifier,JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.operationId'))={operation}
                    """).SingleAsync(token)!=0)return LifecycleFailure(RoomTypeBatchStatus.Duplicate,"REWORK_OPERATION_CONFLICT");
                if(!await ReworkSubmissionWriterAvailable(token))return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_SUBMISSION_WRITER_UNAVAILABLE");
                var state=await ReworkState(id,token);
                if(tag!=WorkflowTag(state.Revision))return new(RoomTypeBatchStatus.Stale);
                if(state.State!="IN_PROGRESS"||row.CompletedAt!=null)return new(RoomTypeBatchStatus.Inactive);
                authority=await ReadTrustedReworkAuthorityAsync(actor,branch,id,root,token);
                var inspection=await InspectionState(row.InspectionId,token);
                if(TrustedReworkExecutionConflict(authority,id,row.InspectionId,task,row.RoomId,branch,inspection.Revision) is {} invalid)
                    return LifecycleFailure(RoomTypeBatchStatus.Unavailable,invalid);
                if(await db.CleaningReworks.CountAsync(w=>w.InspectionId==row.InspectionId&&w.Status!="COMPLETED"&&w.Status!="CANCELLED",token)!=1)
                    return LifecycleFailure(RoomTypeBatchStatus.Inactive,"DUPLICATE_REWORK_SOURCE");
                var defects=await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={id}").ToArrayAsync(token);
                ReworkLifecycleRules.ValidateRemediations(defects,input.Remediations);
                if(await db.Database.SqlQuery<int>($"""
                    SELECT (SELECT COUNT(*) FROM dbo.cleaning_rework_defects WHERE rework_id={id}
                      AND (remediation_note IS NOT NULL OR remediated_by IS NOT NULL OR remediated_at IS NOT NULL))
                      +(SELECT COUNT(*) FROM dbo.fs016_rework_evidence WHERE rework_id={id}) AS Value
                    """).SingleAsync(token)!=0)return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_PREEXISTING_REMEDIATION_UNPROVEN");
                var open=await db.WorkIntervals.AsNoTracking().Where(w=>w.WorkItemKind=="REWORK"&&w.WorkItemId==id&&w.ClosedAt==null).Take(2).ToArrayAsync(token);
                if(open.Length!=1||open[0].UserId!=actor||open[0].BranchId!=branch)return LifecycleFailure(RoomTypeBatchStatus.Inactive,"CURRENT_WORK_INTERVAL_REQUIRED");
                var closed=await time.CloseIntervalAsync(new(actor,actor,branch,"REWORK",id),open[0].IntervalId,open[0].RowVersion,token);
                var now=closed.ClosedAtUtc!.Value;db.ChangeTracker.Clear();
                if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_reworks SET workflow_status='PENDING_REINSPECTION',status='IN_PROGRESS',note={note} WHERE rework_id={id} AND row_version={state.Revision}",token)!=1)
                    return new(RoomTypeBatchStatus.Stale);
                foreach(var remediation in input.Remediations!)
                {
                    var description=CleaningLifecycleRules.Reason(remediation.Note,1);
                    if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.cleaning_rework_defects SET remediation_note={description},remediated_by={actor},remediated_at={now} WHERE rework_id={id} AND defect_id={remediation.DefectId}",token)!=1)
                        throw new InvalidOperationException("REWORK_DEFECT_CHANGED_DURING_SUBMISSION");
                    foreach(var photo in remediation.Photos??[])
                    {
                        if(root==null)throw new ValidationException("Private evidence storage required.");
                        var file=await StoreFeatureEvidence("CLEANING_TASK",task,actor,photo,root,files,token);
                        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.fs016_rework_evidence(rework_id,defect_id,attachment_id) VALUES({id},{remediation.DefectId},{file})",token);
                    }
                }
                var readiness=(await new HotelOps.Api.Services.Rooms.EffectiveRoomReadiness(db).ReadAsync([row.RoomId],now,token))[row.RoomId].Readiness;
                if(readiness is not ("PENDING_INSPECTION" or "PENDING_CLEANING" or "BLOCKED"))throw new InvalidOperationException("REWORK_SUBMISSION_READINESS_CONFLICT");
                remediationDigest=await ReworkRemediationDigest(id,task,actor,root,token)??throw new InvalidOperationException("REWORK_REMEDIATION_UNPROVEN");
                var result=new ReworkCommandResult(id,branch,task,row.InspectionId,actor,"complete","PENDING_REINSPECTION",operation,
                    WorkflowTag((await ReworkState(id,token)).Revision),now,closed.IntervalId,operation,readiness,"PENDING_DELIVERY",ReworkSubmissionContract);
                var json=JsonSerializer.Serialize(result,Json);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    EXEC dbo.fe04_commit_rework_submission @actor={actor},@branch={branch},@rework={id},@operation={operation},
                      @fingerprint={fingerprint},@source_digest={authority!.SourceDigest},@remediation_digest={remediationDigest},@entity={json};
                    """,token);
                return new(RoomTypeBatchStatus.Success,result,result.SourceETag);
            },ct,requireRoomTypeSchema:false,preserveCommitErrors:true,
                featurePermission:token=>ReworkAccess(ticket.UserId,branch,id,"CLEANING_EXECUTE",token),
                eligibility:token=>DueHousekeeper(ticket.UserId,branch,DateTime.MaxValue,token),
                afterUserLocks:token=>time.LockUsersAsync([ticket.UserId],token),
                beforeCommit:async token=>
                {
                    // Replay performed no writes; its protected historical receipt was validated above.
                    if(authority==null)return;
                    var after=await ReworkRemediationDigest(id,task,ticket.UserId,root,token);
                    if(!SameTrustedReworkAuthority(authority,await ReadTrustedReworkAuthorityAsync(ticket.UserId,branch,id,root,token))
                        ||after==null||!after.SequenceEqual(remediationDigest!)||!await ReworkSubmissionWriterAvailable(token))
                        throw new InvalidOperationException("REWORK_SUBMISSION_SOURCE_CHANGED_BEFORE_COMMIT");
                    commitAttempted=true;
                });
        }
        catch(WorkTimeContractException error){return LifecycleTimeFailure(error);}
        catch(Exception error) when(commitAttempted&&error is System.Data.Common.DbException or DbUpdateException)
        {return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_OUTCOME_UNCONFIRMED");}
        finally{if(!commitAttempted)foreach(var file in files)File.Delete(file);}
    }
}
