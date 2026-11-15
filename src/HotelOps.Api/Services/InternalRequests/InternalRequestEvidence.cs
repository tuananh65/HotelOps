using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestEvidenceView(long Id, string Name, string MimeType, long Size, string DownloadPath);
public sealed record InternalRequestEvidenceFile(byte[] Bytes, string MimeType, string Name);

// Existing attachments schema; source is a committed request event, not a client-supplied path.
internal static class InternalRequestEvidence
{
    internal const string EntityType = "INTERNAL_REQUEST_PROGRESS";
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestEvidence.V1' AND CONVERT(nvarchar(20),value)='1')
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.attachments')
                AND name='CK_attachments_entity_type' AND is_disabled=0 AND is_not_trusted=0
                AND definition LIKE '%''INTERNAL_REQUEST_PROGRESS''%') THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;
    internal static (byte[] Bytes, string Extension) Validate(EvidenceUpload upload)
    {
        if (upload == null || string.IsNullOrWhiteSpace(upload.Name) || upload.Name.Length > 200 || upload.Name.Any(char.IsControl)
            || upload.Name.IndexOfAny(['/', '\\', ':']) >= 0 || string.IsNullOrEmpty(upload.Base64) || upload.Base64.Length > 14 * 1024 * 1024)
            throw new ValidationException("Invalid evidence metadata.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(upload.Base64); }
        catch (FormatException) { throw new ValidationException("Invalid evidence encoding."); }
        if (bytes.Length is < 1 or > 10 * 1024 * 1024) throw new ValidationException("Evidence must be at most 10 MiB.");
        var mime = Mime(bytes);
        var extension = Path.GetExtension(upload.Name).ToLowerInvariant();
        if (mime == null || mime != upload.MimeType || mime == "image/png" && extension != ".png"
            || mime == "image/jpeg" && extension is not (".jpg" or ".jpeg") || mime == "application/pdf" && extension != ".pdf")
            throw new ValidationException("Evidence must be JPG, PNG or PDF with matching content, MIME and extension.");
        return (bytes, mime == "image/png" ? ".png" : mime == "image/jpeg" ? ".jpg" : ".pdf");
    }

    private static string? Mime(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
        : bytes.AsSpan().StartsWith(new byte[] {255,216,255}) ? "image/jpeg" : bytes.AsSpan().StartsWith("%PDF-"u8) ? "application/pdf" : null;

    internal static async Task<long> StoreAsync(HotelOpsDbContext db,long source,long actor,DateTime now,EvidenceUpload upload,
        string root,List<string> owned,CancellationToken ct,string sourceType=EntityType)
    {
        if(sourceType is not (EntityType or "INTERNAL_REQUEST_INTAKE"))throw new ValidationException("Unsupported request evidence source.");
        var (bytes,extension)=Validate(upload);
        var directory=Path.GetFullPath(root);Directory.CreateDirectory(directory);
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new ValidationException("Private storage unavailable.");
        var key=Guid.NewGuid().ToString("N")+extension;var path=Path.Combine(directory,key);
        await using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous))
        {owned.Add(path);await stream.WriteAsync(bytes,ct);await stream.FlushAsync(ct);}
        var row=new Attachment{EntityType=sourceType,EntityId=source,FileName=key,OriginalFileName=upload.Name,MimeType=upload.MimeType,
            FileSizeBytes=bytes.Length,StorageKey=key,UploadedBy=actor,UploadedAt=now};
        db.Attachments.Add(row);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();return row.AttachmentId;
    }

    internal static IQueryable<Attachment> Query(HotelOpsDbContext db,long request) =>
        db.Attachments.AsNoTracking().Where(a=>a.DeletedAt==null
            &&db.InternalRequestEvents.Any(e=>e.EventId==a.EntityId&&e.InternalRequestId==request
                &&(a.EntityType==EntityType&&e.EventType=="PROGRESS"||a.EntityType=="INTERNAL_REQUEST_INTAKE"&&e.EventType=="CREATE")));

    internal static async Task<InternalRequestEvidenceFile?> ReadAsync(Attachment row,string root,CancellationToken ct)
    {
        // This feature only writes flat generated keys; reject all alternative path formats.
        var key=row.StorageKey;
        if(string.IsNullOrEmpty(key)||key.IndexOfAny(['/', '\\', ':'])>=0||Path.IsPathRooted(key)
            ||!Guid.TryParseExact(Path.GetFileNameWithoutExtension(key),"N",out _))return null;
        try
        {
            var directory=Path.GetFullPath(root);var path=Path.Combine(directory,key);
            if(!Directory.Exists(directory)||!File.Exists(path)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0
                ||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return null;
            await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous);
            if(stream.Length is <1 or >10*1024*1024||row.FileSizeBytes!=stream.Length)return null;
            var bytes=new byte[(int)stream.Length];await stream.ReadExactlyAsync(bytes,ct);
            var mime=Mime(bytes);if(mime==null||mime!=row.MimeType)return null;
            var name=Path.GetFileName(row.OriginalFileName??row.FileName);
            if(string.IsNullOrWhiteSpace(name)||name.Any(char.IsControl))name="evidence";
            return new(bytes,mime,name);
        }
        catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}catch(ArgumentException){return null;}
    }
}
