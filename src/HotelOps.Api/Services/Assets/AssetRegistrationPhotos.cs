using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

// Existing ASSET attachment discriminator; no public paths or client-chosen storage keys.
internal static class AssetRegistrationPhotos
{
    internal static void Validate(IReadOnlyList<EvidenceUpload>? photos)
    {
        if (photos == null) return;

        foreach (var photo in photos)
        {
            Decode(photo);
            if (photo.MimeType is not ("image/jpeg" or "image/png"))
                throw new ValidationException("MSG23: Asset photos must be JPG or PNG.");
        }
    }

    internal sealed record Limits(int MaxFiles,int MaxMegabytes,string[] Formats);
    internal static async Task<Limits> LimitsAsync(HotelOpsDbContext db,long branch,CancellationToken ct)
    {
        var rows=await db.SystemSettings.AsNoTracking().Where(s=>(s.BranchId==branch||s.BranchId==null)
            &&new[]{"SYS-PAR-07","SYS-PAR-08","SYS-PAR-09"}.Contains(s.SettingKey)).ToArrayAsync(ct);
        string Setting(string key,string fallback){var values=rows.Where(s=>s.SettingKey==key&&s.BranchId==branch).ToArray();
            if(values.Length==0)values=rows.Where(s=>s.SettingKey==key&&s.BranchId==null).ToArray();
            if(values.Length>1)throw new ValidationException("Ambiguous attachment policy.");return values.Length==0?fallback:values[0].SettingValue??"";}
        if(!int.TryParse(Setting("SYS-PAR-08","10"),out var mb)||mb<=0||!int.TryParse(Setting("SYS-PAR-09","5"),out var count)||count<=0)
            throw new ValidationException("Invalid attachment policy.");
        var formats=Setting("SYS-PAR-07","JPG,PNG,PDF").Split([',',';',' ','[',']','"'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Select(s=>s.ToUpperInvariant()).Distinct().ToArray();
        if(formats.Length==0||formats.Any(s=>s is not("JPG" or "JPEG" or "PNG" or "PDF")))throw new ValidationException("Invalid attachment format policy.");
        return new(count,mb,formats.Where(s=>s!="PDF").ToArray());
    }
    private static (byte[] Bytes,string Extension) Decode(EvidenceUpload photo)
    {
        if(photo==null||string.IsNullOrWhiteSpace(photo.Name)||photo.Name.Length>200||photo.Name.Any(char.IsControl)
            ||photo.Name.IndexOfAny(['/', '\\', ':'])>=0||string.IsNullOrEmpty(photo.Base64))throw new ValidationException("Invalid photo metadata.");
        byte[] bytes;try{bytes=Convert.FromBase64String(photo.Base64);}catch(FormatException){throw new ValidationException("Invalid photo encoding.");}
        var extension=Path.GetExtension(photo.Name).ToLowerInvariant();
        var png=bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10});var jpg=bytes.AsSpan().StartsWith(new byte[]{255,216,255});
        if(!(png&&extension==".png"&&photo.MimeType=="image/png"||jpg&&extension is ".jpg" or ".jpeg"&&photo.MimeType=="image/jpeg"))
            throw new ValidationException("Photo extension, MIME and content must match JPG or PNG.");
        return(bytes,png?".png":".jpg");
    }

    internal static async Task StageAsync(HotelOpsDbContext db, long asset, long actor, DateTime now,
        IReadOnlyList<EvidenceUpload>? photos, string privateRoot, EvidenceFileLifetime lifetime, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || asset <= 0) throw new InvalidOperationException("Asset transaction and persisted identity required.");
        Validate(photos);
        if (photos is not { Count: > 0 }) return;
        var branch = await db.Assets.AsNoTracking().Where(a => a.AssetId == asset).Select(a => a.BranchId).SingleAsync(ct);
        var limits=await LimitsAsync(db,branch,ct);
        if(photos.Count>limits.MaxFiles)throw new ValidationException("MSG23: Photo count exceeds SYS-PAR-09.");
        foreach(var photo in photos){var decoded=Decode(photo);var type=decoded.Extension==".png"?"PNG":"JPG";
            if(decoded.Bytes.LongLength>(long)limits.MaxMegabytes*1024*1024||!limits.Formats.Contains(type)&&!(type=="JPG"&&limits.Formats.Contains("JPEG")))
                throw new ValidationException("MSG23: Photo violates configured format or size limits.");}
        var compatible = await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.attachments')
              AND name='CK_attachments_entity_type' AND is_disabled=0 AND is_not_trusted=0 AND definition LIKE '%''ASSET''%')
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct);
        if (compatible != 1) throw new ValidationException("Asset photo storage schema unavailable.");
        var root = Path.GetFullPath(privateRoot);
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new ValidationException("Private photo storage unavailable.");
        var staged = new List<Attachment>();
        foreach (var photo in photos)
        {
            var (bytes, extension) = Decode(photo);
            var key = Guid.NewGuid().ToString("N") + extension;
            var path = Path.Combine(root, key);
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                lifetime.Files.Add(path);
                await stream.WriteAsync(bytes, ct);
                await stream.FlushAsync(ct);
            }
            var attachment = new Attachment { EntityType = "ASSET", EntityId = asset, FileName = key,
                OriginalFileName = photo.Name, MimeType = photo.MimeType, FileSizeBytes = bytes.Length,
                StorageKey = key, UploadedBy = actor, UploadedAt = now };
            db.Attachments.Add(attachment); staged.Add(attachment);
        }
        await db.SaveChangesAsync(ct);

        foreach (var attachment in staged) db.AuditLogs.Add(new AuditLog { EntityType = "attachments", EntityId = attachment.AttachmentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BranchId = branch, ActorUserId = actor, Action = "CREATE", OccurredAt = now,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { attachment.AttachmentId, attachment.EntityType, attachment.EntityId, attachment.OriginalFileName, attachment.MimeType, attachment.FileSizeBytes, attachment.UploadedBy, attachment.UploadedAt }) });
    }
}
