using HotelOps.Api.DTOs.HotelStructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class TurnoverHistoryService
{
    private async Task<PrivateHistoryFileDto?> HistoryFile(IReadOnlyList<HistoryEvidenceDto> evidence, long id, string? root, CancellationToken ct)
    {
        if (id <= 0 || root == null || !evidence.Any(e => e.Id == id)) return null;
        var row = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(a => a.AttachmentId == id && a.DeletedAt == null, ct);
        if (row == null || string.IsNullOrWhiteSpace(row.StorageKey) || Path.IsPathRooted(row.StorageKey)) return null;
        var parts = row.StorageKey.Replace('\\', '/').Split('/');
        if (parts.Any(p => p is "" or "." or ".." || p.Contains(':'))) return null;
        try
        {
            var directory = Path.GetFullPath(root);
            if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return null;
            var path = directory;
            foreach (var part in parts)
            {
                path = Path.Combine(path, part);
                if (!Path.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
            }
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > 10 * 1024 * 1024 || row.FileSizeBytes is { } size && size != stream.Length) return null;
            var bytes = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(bytes, ct);
            var mime = bytes.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}) ? "image/png"
                : bytes.AsSpan().StartsWith(new byte[] {255,216,255}) ? "image/jpeg"
                : bytes.AsSpan().StartsWith("%PDF-"u8) ? "application/pdf" : null;
            if (mime == null || row.MimeType != null && !string.Equals(row.MimeType, mime, StringComparison.OrdinalIgnoreCase)) return null;
            var name = Path.GetFileName(row.OriginalFileName ?? row.FileName);
            if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl)) name = "evidence";
            return new(bytes, mime, name);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (ArgumentException) { return null; }
    }
}
