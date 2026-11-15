using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // UC035 reads do not start a work interval. Execution keeps its independent shift/workday gates.
    public Task<RoomTypeBatchResult> ReadAssignedReworkAsync(SessionReference ticket,long sid,long branch,long id,
        string? root,CancellationToken ct,long? attachmentId=null)
    {
        if(branch<=0||id<=0||attachmentId<=0)throw new ValidationException("Invalid rework identifier.");
        return BatchAsync(ticket,sid,branch,async(actor,token)=>
        {
            if(!await HotelOps.Api.Data.HousekeepingSchema.InstalledAsync(db,token))return new(RoomTypeBatchStatus.SchemaRequired);
            var row=await db.CleaningReworks.AsNoTracking().Where(w=>w.ReworkId==id&&w.AssigneeUserId==actor
                &&w.CleaningTask.Room.BranchId==branch&&w.CleaningTask.Room.IsActive&&w.CleaningTask.Room.Floor.IsActive
                &&(w.CleaningTask.Room.AreaId==null||w.CleaningTask.Room.Area!.IsActive))
                .Select(w=>new{w.ReworkId,w.InspectionId,w.CleaningTaskId,w.CleaningTask.RoomId}).SingleOrDefaultAsync(token);
            if(row==null)return new(RoomTypeBatchStatus.NotFound);
            var proof=await ReadTrustedReworkAuthorityAsync(actor,branch,id,root,token);
            if(proof==null)return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"INSPECTION_AUTHORITY_UNPROVEN");
            var state=await ReworkState(id,token);
            if(state.State is not ("PENDING_ACCEPTANCE" or "ACCEPTED" or "IN_PROGRESS"))
                return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_EXECUTION_READ_CONTRACT_REQUIRED");
            // A legacy remediation writer is not composed into this initial receive/start contract.
            if(await db.Database.SqlQueryRaw<int>("SELECT CASE WHEN COL_LENGTH('dbo.cleaning_rework_defects','remediation_note') IS NOT NULL THEN 1 ELSE 0 END AS Value").SingleAsync(token)==1
                &&await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={id} AND remediation_note IS NOT NULL").SingleAsync(token)>0)
                return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_REMEDIATION_READ_CONTRACT_REQUIRED");
            var defects=await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WHERE rework_id={id}").ToArrayAsync(token);
            if(defects.Length is <1 or >200||await db.InspectionDefects.CountAsync(d=>defects.Contains(d.DefectId)
                &&d.InspectionId==row.InspectionId&&d.ChecklistItem!=null&&d.ChecklistItem.VersionId==proof.Binding.VersionId
                &&db.InspectionResults.Any(r=>r.InspectionId==d.InspectionId&&r.ChecklistItemId==d.ChecklistItemId&&r.Outcome=="FAIL"),token)!=defects.Length)
                return LifecycleFailure(RoomTypeBatchStatus.Unavailable,"REWORK_DEFECT_SOURCE_INVALID");
            var output=new List<ReworkDefectDto>();
            foreach(var defect in defects.Order())
            {
                var description=await db.InspectionDefects.Where(d=>d.DefectId==defect).Select(d=>d.Description).SingleAsync(token);
                var photos=await db.Database.SqlQuery<long>($"""
                    SELECT l.attachment_id AS Value FROM dbo.inspection_defect_attachments l JOIN dbo.attachments a ON a.attachment_id=l.attachment_id
                    WHERE l.defect_id={defect} AND a.deleted_at IS NULL AND a.entity_type='INSPECTION' AND a.entity_id={row.InspectionId}
                    """).ToArrayAsync(token);
                output.Add(new(defect,description,null,photos,[]));
            }
            if(attachmentId is {} file)
            {
                var allowed=output.SelectMany(d=>d.InspectionAttachmentIds).ToArray();
                var metadata=await db.Attachments.AsNoTracking().Where(a=>a.AttachmentId==file&&allowed.Contains(a.AttachmentId)&&a.DeletedAt==null)
                    .Select(a=>new HotelOps.Api.DTOs.HotelStructure.HistoryEvidenceDto(a.AttachmentId,a.EntityType,a.EntityId,
                        a.OriginalFileName??a.FileName,a.MimeType,a.FileSizeBytes,a.UploadedAt,"")).ToArrayAsync(token);
                var download=await HistoryFile(metadata,file,root,token);
                return download==null?new(RoomTypeBatchStatus.NotFound):new(RoomTypeBatchStatus.Success,download);
            }
            return new(RoomTypeBatchStatus.Success,new ReworkExecutionDto(id,branch,row.CleaningTaskId,row.InspectionId,row.RoomId,actor,
                state.State, state.DueAt is {} due?DateTime.SpecifyKind(due,DateTimeKind.Utc):null,WorkflowTag(state.Revision),
                WorkflowTag(proof.Binding.InspectionRevision),output,
                state.State is "PENDING_ACCEPTANCE" or "ACCEPTED" && await AssignedReworkExecutionInstalled(token)
                    ? "ACCEPT_START" : "READ_ONLY",ReworkAuthorityContract.Origin),WorkflowTag(state.Revision));
        },ct,featurePermission:token=>ReworkAccess(ticket.UserId,branch,id,"CLEANING_VIEW",token),
            eligibility:token=>EligibleReworkAssignees(branch,clock.GetUtcNow().UtcDateTime).AnyAsync(u=>u.UserId==ticket.UserId,token));
    }
}
