using HotelOps.Api.Data;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentEvidenceRead
{
    private static IQueryable<HotelOps.Api.Entities.Attachment> Query(HotelOpsDbContext db, long incident) =>
        db.Attachments.AsNoTracking().Where(a => a.EntityType == "INCIDENT" && a.EntityId == incident && a.DeletedAt == null);

    internal static async Task<IActionResult> ListAsync(HotelOpsDbContext db, long actor, long incident, int page, CancellationToken ct)
    {
        if (page < 1 || (long)(page - 1) * 25 > int.MaxValue) return new BadRequestResult();
        var query = Query(db, incident);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.UploadedAt).ThenByDescending(a => a.AttachmentId)
            .Skip((page - 1) * 25).Take(25)
            .Select(a => new { id = a.AttachmentId, name = a.OriginalFileName ?? a.FileName, a.MimeType,
                size = a.FileSizeBytes, note = a.Description, authorId = a.UploadedBy, a.UploadedAt }).ToArrayAsync(ct);
        var source = await db.Incidents.AsNoTracking().SingleAsync(i => i.IncidentId == incident, ct);
        var notesQuery = db.IncidentEvents.AsNoTracking().Where(e => e.IncidentId == incident && e.EventType == "EVIDENCE_ADDED");
        var noteTotal = await notesQuery.CountAsync(ct);
        var notes = await notesQuery.OrderByDescending(e => e.EventId).Skip((page - 1) * 25).Take(25)
            .Select(e => new { id = e.EventId, e.Note, authorId = e.ActorId, at = e.OccurredAt }).ToArrayAsync(ct);
        return new OkObjectResult(new { incidentId = incident, page, total, items = rows, notes, noteTotal,
            version = await IncidentEvidenceWrite.VersionAsync(db, source, ct),
            canAdd = await IncidentEvidenceWrite.CanAddAsync(db, actor, source, ct) });
    }

    // Caller must establish and revalidate current Incident read authority in the same transaction.
    internal static async Task<IActionResult> DownloadAsync(HotelOpsDbContext db, HttpContext http, long incident, long attachment, CancellationToken ct)
    {
        var row = await Query(db, incident).SingleOrDefaultAsync(a => a.AttachmentId == attachment, ct);
        if (row == null) return new NotFoundResult();
        var services = http.RequestServices;
        var root = services.GetRequiredService<IConfiguration>()["Evidence:PrivateRoot"]
            ?? Path.Combine(services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "App_Data", "evidence");
        // Existing reader accepts only flat generated keys, validates size/MIME and rejects reparse points.
        // Legacy external URLs and unrecognized storage layouts are never redirected or dereferenced.
        var file = await InternalRequestEvidence.ReadAsync(row, root, ct);
        if (file == null) return new NotFoundResult();
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        http.Response.Headers["Content-Security-Policy"] = "sandbox";
        http.Response.Headers.CacheControl = "no-store";
        return new FileContentResult(file.Bytes, file.MimeType) { FileDownloadName = file.Name };
    }
}
