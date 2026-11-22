using System.Security.Cryptography;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class EvidenceService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,WorkService work,DocumentService documents,IWebHostEnvironment env,IConfiguration config)
{
    private string Root=>Path.GetFullPath(config["Compact:EvidenceDirectory"]??Path.Combine(env.ContentRootPath,"App_Data","compact-evidence"));
    private async Task Owner(long branch,long? workId,long? documentId,long? assetId,bool write,CancellationToken ct){
        Output.Require(new[]{workId,documentId,assetId}.Count(x=>x!=null)==1,"ONE_OWNER_REQUIRED",400);
        if(workId!=null){
            var w=await work.Load(workId.Value,"read",ct);Output.Require(w.BranchId==branch,"BRANCH_MISMATCH",404);
            if(write){
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
        await using var input=file.OpenReadStream();using var bytes=new MemoryStream();await input.CopyToAsync(bytes,ct);var data=bytes.ToArray();
        var mime=data.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10})?"image/png":data.Length>=3&&data[0]==255&&data[1]==216&&data[2]==255?"image/jpeg":data.AsSpan().StartsWith("%PDF-"u8)?"application/pdf":null;
        Output.Require(mime!=null,"PNG_JPEG_PDF_ONLY",400);var key=Guid.NewGuid().ToString("N");Directory.CreateDirectory(Root);var path=Path.Combine(Root,key);
        await File.WriteAllBytesAsync(path,data,ct);
        try{return await atomic.Run(async()=>{
            await Owner(branch,workId,documentId,assetId,true,ct);
            var a=new Attachment{BranchId=branch,WorkId=workId,DocumentId=documentId,AssetId=assetId,StorageKey=key,OriginalName=Path.GetFileName(file.FileName),MimeType=mime!,ByteLength=data.Length,Sha256=SHA256.HashData(data),UploadedBy=actor.Id};
            db.Attachments.Add(a);await db.SaveChangesAsync(ct);audit.Add("Attachment",a.AttachmentId,"UPLOAD",branch);return (object)Output.Record(a);
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
        Output.Version(a.RowVersion,r.RowVersion);Output.Reason(r.Reason);a.InvalidatedAt=DateTime.UtcNow;a.InvalidationReason=r.Reason;audit.Add("Attachment",id,"INVALIDATE",a.BranchId,after:new{r.Reason});return (object)new{invalidated=true};
    },ct);
}
