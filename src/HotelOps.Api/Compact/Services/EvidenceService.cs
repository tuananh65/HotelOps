using System.Security.Cryptography;
using System.Text.Json;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class EvidenceService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,WorkService work,DocumentService documents,IWebHostEnvironment env,IConfiguration config,TimeProvider clock)
{
    private string Root=>Path.GetFullPath(config["Compact:EvidenceDirectory"]??Path.Combine(env.ContentRootPath,"App_Data","compact-evidence"));
    private async Task Owner(long branch,long? workId,long? documentId,long? assetId,bool write,CancellationToken ct){
        Output.Require(new[]{workId,documentId,assetId}.Count(x=>x!=null)==1,"ONE_OWNER_REQUIRED",400);
        if(write&&!(workId is long continuedWork&&await CleaningContinuationAccess.CanFinishExisting(db,actor,clock,continuedWork,branch,ct)))
            await ShiftCommandAccess.Require(db,actor.Id,branch,clock.GetUtcNow().UtcDateTime,ct);
        if(workId!=null){
            var w=await work.Load(workId.Value,"read",ct);
            if(write&&db.Database.CurrentTransaction!=null)await db.Entry(w).ReloadAsync(ct);
            Output.Require(w.BranchId==branch,"BRANCH_MISMATCH",404);
            if(write){
                Output.Require(!await db.RequestWorkflows.AnyAsync(r=>r.WorkId==w.WorkId&&(r.RequestType==RequestWorkflowService.RefillType||r.RequestType==RequestWorkflowService.ReplenishmentType),ct),"USE_REQUEST_WORKFLOW",403);
                if(w.Kind is "CLEANING" or "REWORK"){
                    await work.Load(w.WorkId,"execute",ct);
                    Output.Require(w.Status=="IN_PROGRESS","CLEANING_NOT_IN_PROGRESS");
                    Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE"&&a.AcceptedAt!=null,ct),"CURRENT_ASSIGNEE_REQUIRED",403);
                }
                if(w.Kind=="MAINTENANCE"){
                    await work.Load(w.WorkId,"execute",ct);
                    Output.Require(w.Status=="IN_PROGRESS","WORK_NOT_EXECUTING");
                    Output.Require(await db.WorkAssignments.AnyAsync(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE"&&a.AcceptedAt!=null,ct),"CURRENT_ASSIGNEE_REQUIRED",403);
                }
                var creator=w.CreatedBy==actor.Id;
                await access.Require(WorkRules.Permission(w.Kind,w.Kind=="INCIDENT"&&creator?"create":"execute"),branch,creator||await work.Owns(w,ct),ct);
                Output.Require((creator&&w.Kind!="MAINTENANCE")||await db.WorkAssignments.AnyAsync(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct),"CURRENT_ASSIGNEE_REQUIRED",403);
                Output.Require(!WorkRules.Terminal.Contains(w.Status),"FINAL_EVIDENCE_IMMUTABLE");
            }
        }
        if(documentId!=null){var d=await documents.Load(documentId.Value,write?"create":"read",ct);Output.Require(d.BranchId==branch,"BRANCH_MISMATCH",404);if(write)Output.Require(d.PostedAt==null,"POSTED_EVIDENCE_IMMUTABLE");}
        if(assetId!=null){var a=await db.Assets.SingleOrDefaultAsync(x=>x.AssetId==assetId&&x.BranchId==branch,ct)??throw new ApiException(404,"ASSET_NOT_FOUND");await access.Require(write?"ASSET_MANAGE":"ASSET_VIEW",branch,ct:ct);}
    }
    public async Task<object> Upload(long branch,long? workId,long? documentId,long? assetId,IFormFile file,CancellationToken ct){
        await Owner(branch,workId,documentId,assetId,true,ct);Output.Require(file.Length is >0 and <=10485760,"FILE_SIZE_MAX_10_MB",400);
        await using var input=file.OpenReadStream();using var bytes=new MemoryStream();var buffer=new byte[81920];int read;
        while((read=await input.ReadAsync(buffer,ct))>0){Output.Require(bytes.Length+read<=10485760,"FILE_SIZE_MAX_10_MB",400);await bytes.WriteAsync(buffer.AsMemory(0,read),ct);}var data=bytes.ToArray();
        Output.Require(data.Length>0&&data.LongLength==file.Length,"INVALID_FILE_LENGTH",400);
        var cleaning=workId!=null&&await db.WorkItems.AnyAsync(w=>w.WorkId==workId&&(w.Kind=="CLEANING"||w.Kind=="REWORK"||w.Kind=="MAINTENANCE"),ct);
        var mime=data.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10})?"image/png":data.Length>=3&&data[0]==255&&data[1]==216&&data[2]==255?"image/jpeg":data.AsSpan().StartsWith("%PDF-"u8)?"application/pdf":null;
        Output.Require(mime!=null,"PNG_JPEG_PDF_ONLY",400);if(cleaning)ValidateCleaningImage(data,mime!);var key=Guid.NewGuid().ToString("N");Directory.CreateDirectory(Root);var path=Path.Combine(Root,key);
        await File.WriteAllBytesAsync(path,data,ct);
        try{return await atomic.Run(async()=>{
            await Owner(branch,workId,documentId,assetId,true,ct);
            var a=new Attachment{BranchId=branch,WorkId=workId,DocumentId=documentId,AssetId=assetId,StorageKey=key,OriginalName=Path.GetFileName(file.FileName),MimeType=mime!,ByteLength=data.Length,Sha256=SHA256.HashData(data),UploadedBy=actor.Id};
            db.Attachments.Add(a);await db.SaveChangesAsync(ct);
            object? binding=null;
            if(cleaning){
                var owner=await db.WorkItems.SingleAsync(w=>w.WorkId==workId,ct);
                var assignment=await db.WorkAssignments.SingleAsync(x=>x.WorkId==workId&&x.AssigneeUserId==actor.Id&&x.AssignmentStatus=="ACTIVE",ct);
                var execution=await CleaningExecution(db,owner.WorkId,assignment,ct);
                binding=new CleaningPhotoBinding(owner.WorkId,owner.RoomId,assignment.AssignmentId,actor.Id,execution.WorkEventId,execution.OccurredAt);
            }
            audit.Add("Attachment",a.AttachmentId,"UPLOAD",branch,after:binding);return (object)Output.Record(a);
        },ct);}catch{File.Delete(path);throw;}
    }
    public async Task<(byte[] Data,string Mime,string Name)> Download(long id,CancellationToken ct){
        var a=await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x=>x.AttachmentId==id&&x.InvalidatedAt==null,ct)??throw new ApiException(404,"ATTACHMENT_NOT_FOUND");
        await Owner(a.BranchId,a.WorkId,a.DocumentId,a.AssetId,false,ct);Output.Require(Guid.TryParseExact(a.StorageKey,"N",out _),"INVALID_STORAGE_KEY",500);
        var path=Path.Combine(Root,a.StorageKey);if(!File.Exists(path))throw new ApiException(404,"EVIDENCE_FILE_MISSING");var bytes=await File.ReadAllBytesAsync(path,ct);
        Output.Require(CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes),a.Sha256),"EVIDENCE_INTEGRITY_FAILURE",500);return(bytes,a.MimeType,a.OriginalName);
    }
    public Task<object> Invalidate(long id,ActionRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var a=await db.Attachments.FindAsync([id],ct)??throw new ApiException(404,"ATTACHMENT_NOT_FOUND");await Owner(a.BranchId,a.WorkId,a.DocumentId,a.AssetId,true,ct);
        if(a.WorkId is long workId){
            var reports=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==workId&&(e.EventType=="COMPLETE"||e.EventType=="MAINTENANCE_UNREPAIRABLE")&&e.SnapshotJson!=null).Select(e=>e.SnapshotJson!).ToArrayAsync(ct);
            Output.Require(!reports.Any(json=>ReportContainsPhoto(json,id)),"SUBMITTED_PHOTO_IMMUTABLE");
        }
        Output.Version(a.RowVersion,r.RowVersion);Output.Reason(r.Reason);a.InvalidatedAt=DateTime.UtcNow;a.InvalidationReason=r.Reason;audit.Add("Attachment",id,"INVALIDATE",a.BranchId,after:new{r.Reason});return (object)new{invalidated=true};
    },ct);

    internal sealed record CleaningPhotoBinding(long WorkId,long? RoomId,long AssignmentId,long UserId,long ExecutionEventId,DateTime ExecutionStartedAt);

    internal static async Task<WorkEvent> CleaningExecution(HotelOpsCompactDbContext context,long workId,WorkAssignment assignment,CancellationToken ct)
    {
        // Resume belongs to the same execution; a new START or assignment requires fresh photos.
        return await context.WorkEvents.AsNoTracking().Where(e=>e.WorkId==workId&&e.EventType=="START"&&e.OccurredAt>=assignment.AssignedAt)
            .OrderByDescending(e=>e.WorkEventId).FirstOrDefaultAsync(ct)
            ?? throw new ApiException(409,"CLEANING_EXECUTION_REQUIRED");
    }

    internal static bool ReportContainsPhoto(string json,long id)
    {
        using var report=JsonDocument.Parse(json);
        return report.RootElement.TryGetProperty("attachmentIds",out var ids)&&ids.ValueKind==JsonValueKind.Array
            &&ids.EnumerateArray().Any(item=>item.TryGetInt64(out var value)&&value==id);
    }

    internal static void ValidateCleaningImage(byte[] data,string mime)
    {
        Output.Require(data.Length is >0 and <=10485760,"FILE_SIZE_MAX_10_MB",400);
        var png=data.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10});
        var jpeg=data.Length>=4&&data[0]==255&&data[1]==216&&data[^2]==255&&data[^1]==217;
        Output.Require((mime=="image/png"&&png)||(mime=="image/jpeg"&&jpeg),"CLEANING_IMAGE_PNG_JPEG_ONLY",400);
        try{
            using var stream=new MemoryStream(data,0,data.Length,false,true);
            using var image=XImage.FromStream(stream);
            Output.Require(image.PixelWidth>0&&image.PixelHeight>0&&(long)image.PixelWidth*image.PixelHeight<=40000000,"CLEANING_IMAGE_DIMENSIONS",400);
            // Drawing and saving also prepares PNG pixel data, rejecting corrupt compressed payloads.
            using var document=new PdfDocument();var page=document.AddPage();
            using(var graphics=XGraphics.FromPdfPage(page))graphics.DrawImage(image,0,0,1,1);
            using var output=new MemoryStream();document.Save(output,false);
        }catch(ApiException){throw;}
        catch(Exception error) when(error is not OutOfMemoryException and not OperationCanceledException){throw new ApiException(400,"INVALID_CLEANING_IMAGE");}
    }

    internal static async Task ValidateStoredCleaningImage(Attachment attachment,string root,CancellationToken ct)
    {
        Output.Require(Guid.TryParseExact(attachment.StorageKey,"N",out _),"INVALID_STORAGE_KEY",500);
        var path=Path.Combine(root,attachment.StorageKey);
        Output.Require(File.Exists(path),"EVIDENCE_FILE_MISSING",409);
        Output.Require(new FileInfo(path).Length==attachment.ByteLength&&attachment.ByteLength is >0 and <=10485760,"EVIDENCE_INTEGRITY_FAILURE",409);
        var data=await File.ReadAllBytesAsync(path,ct);
        Output.Require(CryptographicOperations.FixedTimeEquals(SHA256.HashData(data),attachment.Sha256),"EVIDENCE_INTEGRITY_FAILURE",409);
        ValidateCleaningImage(data,attachment.MimeType);
    }
}
