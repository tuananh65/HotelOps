using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;
public sealed partial class RoomTypeCreationService
{
    private async Task<bool> InspectionEvidenceRevisionInstalled(CancellationToken ct)
        => await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Housekeeping.EvidenceRevision.V1' AND CONVERT(nvarchar(128),value)='314DBD336CED88E2DCFDCF6762F8126BFA95D349247ACADA7294F83F19B1F7AD') AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_hk_attachment_revision_v1','TR_hk_defect_files_v1','TR_hk_result_files_v1') AND is_disabled=0)=3 THEN 1 ELSE 0 END AS Value").SingleAsync(ct)==1;
    // Explicitly closed until compatible accepted participants exist. No config toggle can turn a proposal into authority.
    internal static readonly string[] RejectWriteDependencies = ["INSPECTION_NOTIFICATION_STAGING_UNACCEPTED",
        "ROOM_STATUS_PROJECTION_CONTRACT_REQUIRED","DURABLE_INSPECTION_RECEIPT_CONTRACT_REQUIRED"];
    internal static readonly string[] ReworkWriteDependencies = ["AUTHORITATIVE_FAILED_RECEIPT_CONTRACT_REQUIRED",
        "REWORK_SOURCE_BINDING_CONTRACT_REQUIRED","REWORK_REQUESTED_STAGING_UNACCEPTED"];

    public Task<RoomTypeBatchResult> RejectInspectionAsync(SessionReference ticket,long sid,long branch,long id,
        string tag,string key,RejectInspectionRequest input,CancellationToken ct,bool preview=false)
    {
        if(branch<=0||id<=0||!CleaningLifecycleRules.StrongTag(tag))throw new ValidationException("Positive source identity and strong If-Match required.");
        if(!preview)CleaningLifecycleRules.Operation(key);
        var command=new RejectInspectionLoadCommand(branch,id,new(input.Reason,input.Confirmed,tag));
        return BatchAsync(ticket,sid,branch,async(actor,token)=>
        {
            var loaded=await LoadRejectInspectionSourceAsync(ticket,sid,command,token);
            if(loaded.Status!=RoomTypeBatchStatus.Success)return new(loaded.Status,new InspectionMutationBlocked(loaded.Blocker??"REJECT_SOURCE_INVALID",loaded.Errors??[]));
            var source=loaded.Source!;
            if(preview)return new(RoomTypeBatchStatus.Success,new RejectInspectionPreview(id,branch,source.Domain.Inspection.RoomId,
                source.Domain.CleaningTask.CleaningTaskId,source.Domain.Version.VersionId,source.Domain.WorkflowState!,tag,
                source.Domain.Defects.Select(d=>d.DefectId).ToArray(),source.Evidence.Where(e=>e.DeletedAt==null).Select(e=>e.AttachmentId).Distinct().ToArray(),false,RejectWriteDependencies),tag);
            if(RejectWriteDependencies.Length!=0)return new(RoomTypeBatchStatus.Unavailable,new InspectionMutationBlocked("INSPECTION_INTEGRATION_REQUIRED",RejectWriteDependencies));
            // Do not remove the gate without accepted lifecycle projection, staging and replay implementation.
            await InspectionConclusionSql.RejectAsync(db,source,command.Input,token);
            await BatchAuditAsync(actor,branch,"room_inspections",id,new{state="PENDING_CONCLUSION"},new{state="FAILED",input.Reason},["workflowStatus","result","cleaningTask"],token);
            throw new InvalidOperationException("INSPECTION_ATOMIC_PARTICIPANTS_REQUIRED"); // no partial commit under any future gate edit
        },ct,requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM");
    }

    public Task<RoomTypeBatchResult> ReadRejectInspectionPreviewAsync(SessionReference ticket,long sid,long branch,long id,CancellationToken ct)
        => BatchAsync(ticket,sid,branch,async(_,token)=>
        {
            if(!await HousekeepingInstalled(token))return new(RoomTypeBatchStatus.SchemaRequired);
            var row=await db.RoomInspections.AsNoTracking().SingleOrDefaultAsync(i=>i.InspectionId==id&&i.Room.BranchId==branch,token);
            if(row==null)return new(RoomTypeBatchStatus.NotFound);
            var state=await InspectionState(id,token);
            var loaded=await LoadRejectInspectionSourceAsync(ticket,sid,new(branch,id,new("Preview pending rejection",true,WorkflowTag(state.Revision))),token);
            if(loaded.Status!=RoomTypeBatchStatus.Success)return new(loaded.Status,new InspectionMutationBlocked(loaded.Blocker??"REJECT_SOURCE_INVALID",loaded.Errors??[]));
            var s=loaded.Source!;
            return new(RoomTypeBatchStatus.Success,new RejectInspectionPreview(id,branch,row.RoomId,s.Domain.CleaningTask.CleaningTaskId,row.VersionId,
                s.Domain.WorkflowState!,WorkflowTag(s.Domain.Revision),s.Domain.Defects.Select(d=>d.DefectId).ToArray(),s.Evidence.Where(e=>e.DeletedAt==null).Select(e=>e.AttachmentId).Distinct().ToArray(),false,RejectWriteDependencies),WorkflowTag(s.Domain.Revision));
        },ct,requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM");

    private Task<RoomTypeBatchResult> RequestAuthoritativeReworkAsync(SessionReference ticket,long sid,long branch,long id,
        string tag,RequestReworkRequest input,CancellationToken ct)
    {
        if(id<=0||input.AssigneeId<=0||!CleaningLifecycleRules.StrongTag(tag)||input.DefectIds==null||input.DefectIds.Count is <1 or >200
            ||input.DefectIds.Distinct().Count()!=input.DefectIds.Count||input.DefectIds.Any(x=>x<=0))throw new ValidationException("Invalid rework source/request.");
        WorkflowReason(input.Reason);WorkflowDue(input.DueAt,clock.GetUtcNow().UtcDateTime);
        return BatchAsync(ticket,sid,branch,async(_,token)=>
        {
            var result=await ReworkSourceSnapshotAsync(branch,id,tag,token);
            if(result.Status!=RoomTypeBatchStatus.Success)return result;
            var source=(ReworkSourcePreview)result.Value!;
            if(input.DefectIds.Any(d=>!source.DefectIds.Contains(d)))return new(RoomTypeBatchStatus.InvalidReference);
            if(await db.CleaningReworks.AnyAsync(w=>w.InspectionId==id&&w.Status!="COMPLETED"&&w.Status!="CANCELLED",token))return new(RoomTypeBatchStatus.Duplicate);
            if(ReworkWriteDependencies.Length!=0)
                return new(RoomTypeBatchStatus.Unavailable,new InspectionMutationBlocked("AUTHORITATIVE_FAILED_SOURCE_UNAVAILABLE",ReworkWriteDependencies));
            if(!await DueHousekeeper(input.AssigneeId,branch,input.DueAt,token))return new(RoomTypeBatchStatus.InvalidReference);
            var rework=await InspectionConclusionSql.CreateReworkAsync(db,branch,id,Convert.FromHexString(source.ETag.Trim('"')),
                input.AssigneeId,input.DueAt,input.Reason,input.DefectIds,token);
            await BatchAuditAsync(ticket.UserId,branch,"cleaning_reworks",rework,null,new{inspectionId=id,input.AssigneeId,input.DefectIds},["inspection","defects","assignee"],token);
            throw new InvalidOperationException("REWORK_ATOMIC_SOURCE_RECEIPT_AND_STAGING_REQUIRED");
        },ct,additionalUsers:[input.AssigneeId],requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM");
    }
}
