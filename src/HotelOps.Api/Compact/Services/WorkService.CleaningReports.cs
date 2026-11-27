using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkService
{
    private static bool CleaningReportMatches(string? snapshot,IReadOnlyList<long>? ids)
    {
        if(snapshot==null||ids==null)return false;
        try{
            using var report=JsonDocument.Parse(snapshot);
            return report.RootElement.TryGetProperty("attachmentIds",out var recorded)
                &&recorded.EnumerateArray().Select(x=>x.GetInt64()).Order().SequenceEqual(ids.Order());
        }catch(JsonException){return false;}
    }
    private readonly string? cleaningEvidenceRoot;

    // DI selects this superset constructor; the original constructor remains available to existing callers.
    public WorkService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,
        TimeProvider clock,NotificationService notifications,DocumentService documents,RoomReadinessService readiness,
        StaffSupplyService supplies,RequestWorkflowService requests,IWebHostEnvironment environment,IConfiguration configuration)
        : this(db,access,actor,atomic,audit,clock,notifications,documents,readiness,supplies,requests)
    {
        cleaningEvidenceRoot=Path.GetFullPath(configuration["Compact:EvidenceDirectory"]
            ??Path.Combine(environment.ContentRootPath,"App_Data","compact-evidence"));
    }

    private async Task<string> ValidateCleaningCompletionReport(WorkItem work,WorkAssignment assignment,ActionRequest request,CancellationToken ct)
    {
        Output.Require(work.Kind is "CLEANING" or "REWORK","CLEANING_KIND_REQUIRED",400);
        Output.Require(work.Status=="IN_PROGRESS","CLEANING_NOT_IN_PROGRESS");
        Output.Require(assignment.WorkId==work.WorkId&&assignment.BranchId==work.BranchId
            &&assignment.AssigneeUserId==actor.Id&&assignment.AssignmentStatus=="ACTIVE"&&assignment.AcceptedAt!=null,"CURRENT_ASSIGNEE_REQUIRED",403);
        var ids=request.AttachmentIds?.ToArray()??[];
        Output.Require(ids.Length>0,"CLEANING_PHOTO_REQUIRED",400);
        Output.Require(ids.All(id=>id>0)&&ids.Distinct().Count()==ids.Length,"INVALID_CLEANING_PHOTOS",400);
        var execution=await EvidenceService.CleaningExecution(db,work.WorkId,assignment,ct);
        var photos=await db.Attachments.AsNoTracking().Where(a=>ids.Contains(a.AttachmentId)&&a.WorkId==work.WorkId
            &&a.BranchId==work.BranchId&&a.UploadedBy==actor.Id&&a.InvalidatedAt==null
            &&a.CreatedAt>=assignment.AssignedAt&&a.CreatedAt>=execution.OccurredAt).ToArrayAsync(ct);
        Output.Require(photos.Length==ids.Length,"INVALID_CLEANING_PHOTOS",400);
        var keys=ids.Select(id=>id.ToString()).ToArray();
        var uploads=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="Attachment"&&a.Action=="UPLOAD"
            &&a.BranchId==work.BranchId&&a.ActorUserId==actor.Id&&keys.Contains(a.EntityKey)).ToArrayAsync(ct);
        foreach(var photo in photos){
            var upload=uploads.SingleOrDefault(a=>a.EntityKey==photo.AttachmentId.ToString());
            EvidenceService.CleaningPhotoBinding? binding=null;
            try{if(upload?.AfterJson!=null)binding=JsonSerializer.Deserialize<EvidenceService.CleaningPhotoBinding>(upload.AfterJson);}
            catch(JsonException){throw new ApiException(409,"CLEANING_PHOTO_EXECUTION_MISMATCH");}
            Output.Require(binding!=null&&binding.WorkId==work.WorkId&&binding.RoomId==work.RoomId
                &&binding.AssignmentId==assignment.AssignmentId&&binding.UserId==actor.Id
                &&binding.ExecutionEventId==execution.WorkEventId,"CLEANING_PHOTO_EXECUTION_MISMATCH",409);
            Output.Require(cleaningEvidenceRoot!=null,"EVIDENCE_STORAGE_NOT_CONFIGURED",500);
            await EvidenceService.ValidateStoredCleaningImage(photo,cleaningEvidenceRoot!,ct);
        }
        return JsonSerializer.Serialize(new{reportType="cleaning-completion",attachmentIds=ids,
            workId=work.WorkId,roomId=work.RoomId,assignmentId=assignment.AssignmentId,
            userId=actor.Id,executionEventId=execution.WorkEventId,executionStartedAt=execution.OccurredAt,
            submittedAt=Now,note=request.Reason});
    }
}
