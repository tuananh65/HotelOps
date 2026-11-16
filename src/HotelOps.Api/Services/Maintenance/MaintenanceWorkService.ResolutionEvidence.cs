using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService
{
    private sealed record ResolutionEvidenceProof(long AttachmentId, long IncidentId,
        string Name, string MimeType, long Size, string Sha256, long UploadedBy, DateTime UploadedAt);

    private async Task<IReadOnlyList<ResolutionEvidenceProof>> ValidateResolutionEvidenceAsync(
        SubmitMaintenanceResultRequest input, Incident incident, CancellationToken ct)
    {
        var ids = MaintenanceResolution.EvidenceIds(input.ResultAttachmentIds);
        if (ids.Length == 0) return [];
        if (_dbContext.Database.CurrentTransaction == null) throw new Dev2AccessDenied(503);
        // Selecting existing evidence does not grant upload or download authority.
        // Require the independently authorized Incident detail scope at submission.
        var scope = await IncidentReadAccess.ReadAsync(_dbContext, TrustedActorId, incident.BranchId, ct);
        if (!await scope.Apply(_dbContext.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == incident.IncidentId, ct))
            throw new Dev2AccessDenied(403);
        var rows = await _dbContext.Attachments.AsNoTracking().Where(a => ids.Contains(a.AttachmentId)
                && a.EntityType == "INCIDENT" && a.EntityId == incident.IncidentId && a.DeletedAt == null)
            .OrderBy(a => a.AttachmentId).ToArrayAsync(ct);
        if (rows.Length != ids.Length)
            throw new ValidationException("Result evidence is unavailable for this Incident.");
        var services = _authorityHttp?.HttpContext?.RequestServices ?? throw new Dev2AccessDenied(503);
        var root = services.GetRequiredService<IConfiguration>()["Evidence:PrivateRoot"]
            ?? Path.Combine(services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, "App_Data", "evidence");
        var result = new List<ResolutionEvidenceProof>();
        foreach (var row in rows)
        {
            // Existing reader validates private generated keys, size, MIME and reparse points.
            // No client path/URL is accepted, and no new file is written by submission.
            var file = await InternalRequestEvidence.ReadAsync(row, root, ct)
                ?? throw new ValidationException("Result evidence content is unavailable or invalid.");
            result.Add(new(row.AttachmentId, incident.IncidentId, file.Name, file.MimeType, file.Bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(file.Bytes)), row.UploadedBy, row.UploadedAt));
        }
        return result;
    }
}
