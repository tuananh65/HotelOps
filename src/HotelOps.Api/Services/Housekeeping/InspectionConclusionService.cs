using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;
public sealed partial class RoomTypeCreationService
{
    private async Task<bool> InspectionEvidenceRevisionInstalled(CancellationToken ct)
        => await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.Housekeeping.EvidenceRevision.V1' AND CONVERT(nvarchar(128),value)='E94CD891B0EDE6AD98384B91B35B6DC395E70E53D3CAD994854F4B7237C61CAC') AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_hk_attachment_revision_v1','TR_hk_defect_files_v1','TR_hk_result_files_v1') AND is_disabled=0)=3 THEN 1 ELSE 0 END AS Value").SingleAsync(ct)==1;
    // Explicitly closed until compatible accepted participants exist. No config toggle can turn a proposal into authority.
    internal static readonly string[] RejectWriteDependencies = ["INSPECTION_NOTIFICATION_STAGING_UNACCEPTED",
        "ROOM_STATUS_PROJECTION_CONTRACT_REQUIRED","DURABLE_INSPECTION_RECEIPT_CONTRACT_REQUIRED","PRIVATE_EVIDENCE_IMMUTABILITY_UNACCEPTED"];
    internal static readonly string[] ReworkWriteDependencies = ["AUTHORITATIVE_FAILED_RECEIPT_CONTRACT_REQUIRED",
        "REWORK_SOURCE_BINDING_CONTRACT_REQUIRED","REWORK_REQUESTED_STAGING_UNACCEPTED","PRIVATE_EVIDENCE_IMMUTABILITY_UNACCEPTED"];

    public async Task<RoomTypeBatchResult> RejectInspectionAsync(SessionReference ticket,long sid,long branch,long id,
        string tag,string key,RejectInspectionRequest input,CancellationToken ct,bool preview=false)
    {
        if(branch<=0||id<=0||!CleaningLifecycleRules.StrongTag(tag))throw new ValidationException("Positive source identity and strong If-Match required.");
        if(!preview)CleaningLifecycleRules.Operation(key);
        InspectionRecipientRoster? discovered = null;
        var composed = false;
        if(!preview&&RejectWriteDependencies.Length==0)
        {
            var readiness=await DiscoverAuthorizedRosterAsync(ticket,sid,branch,id,null,HotelOps.Api.Services.Notifications.Fe05NotificationKind.InspectionFailed,ct);
            if(readiness.Denied is {} denied)return new(denied);discovered=readiness.Roster;
        }
        var command=new RejectInspectionLoadCommand(branch,id,new(input.Reason,input.Confirmed,tag));
        return await BatchAsync(ticket,sid,branch,async(actor,token)=>
        {
            // Reserve/replay precedes state-sensitive preflight. Existing gates remain closed.
            if (!preview && RejectWriteDependencies.Length == 0)
            {
                if (inspectionStaging == null || inspectionCommitGuard == null)
                    return new(RoomTypeBatchStatus.Unavailable);
                var json = await ComposeRejectInspectionAsync(ticket,sid,branch,id,tag,
                    CleaningLifecycleRules.Operation(key),input,inspectionStaging,inspectionCommitGuard,token);
                composed = true;
                using var receipt = System.Text.Json.JsonDocument.Parse(json);
                return new(RoomTypeBatchStatus.Success,receipt.RootElement.Clone(),receipt.RootElement.GetProperty("inspectionETag").GetString());
            }
            var loaded=await PrepareRejectInCallerTransactionAsync(ticket,sid,command,token);
            if(loaded.Status!=RoomTypeBatchStatus.Success)return new(loaded.Status,new InspectionMutationBlocked(loaded.Blocker??"REJECT_SOURCE_INVALID",loaded.Errors??[]));
            var source=loaded.Source!;
            if(preview)return new(RoomTypeBatchStatus.Success,new RejectInspectionPreview(id,branch,source.Domain.Inspection.RoomId,
                source.Domain.CleaningTask.CleaningTaskId,source.Domain.Version.VersionId,source.Domain.WorkflowState!,tag,
                source.Domain.Defects.Select(d=>d.DefectId).ToArray(),source.Evidence.Where(e=>e.DeletedAt==null).Select(e=>e.AttachmentId).Distinct().ToArray(),false,RejectWriteDependencies),tag);
            if(RejectWriteDependencies.Length!=0)return new(RoomTypeBatchStatus.Unavailable,new InspectionMutationBlocked("INSPECTION_INTEGRATION_REQUIRED",RejectWriteDependencies));
            throw new InvalidOperationException("INSPECTION_COMPOSITION_REQUIRED");
        },ct,additionalUsers:discovered?.Users,onUsersLocked:discovered==null?null:(locked,token)=>inspectionUsers!.RetainLockedUsersAsync(discovered,locked,token),
            requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM",beforeCommit:token => composed
            ? inspectionCommitGuard!.VerifyBeforeCommitAsync(db,token) : Task.CompletedTask,preserveCommitErrors:true);
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

    private async Task<RoomTypeBatchResult> RequestAuthoritativeReworkAsync(SessionReference ticket,long sid,long branch,long id,
        string tag,RequestReworkRequest input,CancellationToken ct,string? operationKey = null)
    {
        if(id<=0||input.AssigneeId<=0||!CleaningLifecycleRules.StrongTag(tag)||input.DefectIds==null||input.DefectIds.Count is <1 or >200
            ||input.DefectIds.Distinct().Count()!=input.DefectIds.Count||input.DefectIds.Any(x=>x<=0))throw new ValidationException("Invalid rework source/request.");
        if(input.DueAt.Kind!=DateTimeKind.Utc)throw new ValidationException("Due time must be UTC.");
        InspectionRecipientRoster? discovered = null;
        var operationId = CleaningLifecycleRules.Operation(operationKey ?? "");
        var composed = false;
        if(ReworkWriteDependencies.Length==0)
        {
            var readiness=await DiscoverAuthorizedRosterAsync(ticket,sid,branch,id,input.AssigneeId,HotelOps.Api.Services.Notifications.Fe05NotificationKind.CleaningReworkRequested,ct);
            if(readiness.Denied is {} denied)return new(denied);discovered=readiness.Roster;
        }
        WorkflowReason(input.Reason); // Due is checked only on a fresh operation, never before completed replay.
        return await BatchAsync(ticket,sid,branch,async(_,token)=>
        {
            if (ReworkWriteDependencies.Length == 0)
            {
                if (inspectionStaging == null || inspectionCommitGuard == null)
                    return new(RoomTypeBatchStatus.Unavailable);
                // Private evidence root/immutability is deliberately not invented here.
                // The immutable storage acceptance gate must be closed before activation.
                var json = await ComposeInspectionReworkAsync(ticket,sid,branch,id,tag,operationId,
                    input,null,inspectionStaging,inspectionCommitGuard,token);
                composed = true;
                using var receipt = System.Text.Json.JsonDocument.Parse(json);
                return new(RoomTypeBatchStatus.Success,receipt.RootElement.Clone(),receipt.RootElement.GetProperty("inspectionETag").GetString());
            }
            var result=await PrepareReworkInCallerTransactionAsync(ticket,sid,branch,id,tag,token);
            if(result.Status!=RoomTypeBatchStatus.Success)return result;
            var source=(ReworkSourcePreview)result.Value!;
            if(input.DefectIds.Any(d=>!source.DefectIds.Contains(d)))return new(RoomTypeBatchStatus.InvalidReference);
            if(await db.CleaningReworks.AnyAsync(w=>w.InspectionId==id&&w.Status!="COMPLETED"&&w.Status!="CANCELLED",token))return new(RoomTypeBatchStatus.Duplicate);
            if(ReworkWriteDependencies.Length!=0)
                return new(RoomTypeBatchStatus.Unavailable,new InspectionMutationBlocked("AUTHORITATIVE_FAILED_SOURCE_UNAVAILABLE",ReworkWriteDependencies));
            throw new InvalidOperationException("REWORK_COMPOSITION_REQUIRED");
        },ct,additionalUsers:discovered?.Users??[input.AssigneeId],
            onUsersLocked:discovered==null?null:(locked,token)=>inspectionUsers!.RetainLockedUsersAsync(discovered,locked,token),requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM",
            beforeCommit:token => composed ? inspectionCommitGuard!.VerifyBeforeCommitAsync(db,token) : Task.CompletedTask,preserveCommitErrors:true);
    }
}
