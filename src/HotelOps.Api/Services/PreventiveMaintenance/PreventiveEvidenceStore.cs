using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

internal static class PreventiveEvidenceStore
{
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.PreventiveEvidence.V1'
          AND CONVERT(nvarchar(10),value)='1') AND EXISTS(SELECT 1 FROM sys.check_constraints
          WHERE parent_object_id=OBJECT_ID('dbo.attachments') AND name='CK_attachments_entity_type'
          AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%''OCCURRENCE''%') THEN 1 ELSE 0 END AS Value
        """).AnyAsync(x=>x==1,ct);
    internal static IQueryable<Attachment> Query(HotelOpsDbContext db,long id)=>db.Attachments.AsNoTracking()
        .Where(a=>a.EntityType=="OCCURRENCE"&&a.EntityId==id&&a.DeletedAt==null);
    internal static async Task<long[]> StageAsync(HotelOpsDbContext db,long id,long actor,DateTime now,IReadOnlyList<EvidenceUpload>? uploads,
        string root,EvidenceFileLifetime lifetime,CancellationToken ct)
    {
        if(uploads is not {Count:>0})return [];
        if(uploads.Count>5)throw new InternalRequestReadException(400,"MSG23");
        if(!await InstalledAsync(db,ct))throw new InternalRequestReadException(503,"OCCURRENCE_EVIDENCE_STORAGE_UNAVAILABLE");
        var rows=new List<Attachment>();
        foreach(var upload in uploads)
        {
            byte[] bytes;string extension;
            try{(bytes,extension)=InternalRequestEvidence.Validate(upload);}
            catch(System.ComponentModel.DataAnnotations.ValidationException){throw new InternalRequestReadException(400,"MSG23");}
            var directory=Path.GetFullPath(root);Directory.CreateDirectory(directory);
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InternalRequestReadException(503,"PRIVATE_STORAGE_UNAVAILABLE");
            var key=Guid.NewGuid().ToString("N")+extension;var path=Path.Combine(directory,key);
            await using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous))
            {lifetime.Files.Add(path);await stream.WriteAsync(bytes,ct);await stream.FlushAsync(ct);}
            var attachment=new Attachment{EntityType="OCCURRENCE",EntityId=id,FileName=key,OriginalFileName=upload.Name,
                MimeType=upload.MimeType,FileSizeBytes=bytes.Length,StorageKey=key,UploadedBy=actor,UploadedAt=now};
            rows.Add(attachment);db.Attachments.Add(attachment);
        }
        await db.SaveChangesAsync(ct);return rows.Select(a=>a.AttachmentId).ToArray();
    }
}
