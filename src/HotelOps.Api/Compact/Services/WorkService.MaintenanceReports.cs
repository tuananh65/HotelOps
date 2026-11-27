using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private async Task<string> ValidateMaintenanceCompletionReport(WorkItem work,WorkAssignment? assignment,ActionRequest request,CancellationToken ct)
    {
        Output.Require(work.Kind=="MAINTENANCE"&&work.Status=="IN_PROGRESS","WORK_NOT_EXECUTING");
        Output.Require(assignment!=null&&assignment.WorkId==work.WorkId&&assignment.BranchId==work.BranchId
            &&assignment.AssigneeUserId==actor.Id&&assignment.AssignmentStatus=="ACTIVE"&&assignment.AcceptedAt!=null,"CURRENT_ASSIGNEE_REQUIRED",403);
        var ids=request.AttachmentIds?.ToArray()??[];
        ValidateMaintenancePhotoIds(ids);
        var execution=await EvidenceService.CleaningExecution(db,work.WorkId,assignment!,ct);
        var photos=await db.Attachments.AsNoTracking().Where(a=>ids.Contains(a.AttachmentId)&&a.WorkId==work.WorkId
            &&a.BranchId==work.BranchId&&a.UploadedBy==actor.Id&&a.InvalidatedAt==null
            &&a.CreatedAt>=assignment!.AssignedAt&&a.CreatedAt>=execution.OccurredAt).ToArrayAsync(ct);
        Output.Require(photos.Length==ids.Length,"INVALID_MAINTENANCE_PHOTOS",400);
        var keys=ids.Select(id=>id.ToString()).ToArray();
        var uploads=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="Attachment"&&a.Action=="UPLOAD"
            &&a.BranchId==work.BranchId&&a.ActorUserId==actor.Id&&keys.Contains(a.EntityKey)).ToArrayAsync(ct);
        foreach(var photo in photos){
            var upload=uploads.SingleOrDefault(a=>a.EntityKey==photo.AttachmentId.ToString());
            EvidenceService.CleaningPhotoBinding? binding=null;
            try{if(upload?.AfterJson!=null)binding=JsonSerializer.Deserialize<EvidenceService.CleaningPhotoBinding>(upload.AfterJson);}
            catch(JsonException){throw new ApiException(409,"MAINTENANCE_PHOTO_EXECUTION_MISMATCH");}
            Output.Require(binding!=null&&binding.WorkId==work.WorkId&&binding.RoomId==work.RoomId
                &&binding.AssignmentId==assignment!.AssignmentId&&binding.UserId==actor.Id
                &&binding.ExecutionEventId==execution.WorkEventId,"MAINTENANCE_PHOTO_EXECUTION_MISMATCH");
            Output.Require(cleaningEvidenceRoot!=null,"EVIDENCE_STORAGE_NOT_CONFIGURED",500);
            await EvidenceService.ValidateStoredCleaningImage(photo,cleaningEvidenceRoot!,ct);
        }
        return JsonSerializer.Serialize(new{reportType="maintenance-completion",attachmentIds=ids,
            workId=work.WorkId,roomId=work.RoomId,assignmentId=assignment!.AssignmentId,userId=actor.Id,
            executionEventId=execution.WorkEventId,executionStartedAt=execution.OccurredAt,submittedAt=Now,note=request.Reason});
    }

    internal static void ValidateMaintenancePhotoIds(IReadOnlyList<long> ids)
    {
        Output.Require(ids.Count>0,"MAINTENANCE_PHOTO_REQUIRED",400);
        Output.Require(ids.All(id=>id>0)&&ids.Distinct().Count()==ids.Count,"INVALID_MAINTENANCE_PHOTOS",400);
    }
}
