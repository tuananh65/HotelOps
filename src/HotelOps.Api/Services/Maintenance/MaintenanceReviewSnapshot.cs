using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceReviewSnapshot
{
    internal static MaintenanceReviewDto? Parse(AuditLog audit)
    {
        if (audit.AuditId <= 0 || MaintenanceSubmissionSource.Parse(audit) == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(audit.NewValues!);
            var root = doc.RootElement;
            if (root.GetProperty("submitted_materials").ValueKind != JsonValueKind.Array) return null;
            var ids = root.GetProperty("submitted_evidence").EnumerateArray()
                .Select(e => e.GetProperty("AttachmentId").GetInt64()).ToArray();
            if (ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length) return null;
            return new(audit.AuditId, DateTime.SpecifyKind(audit.OccurredAt, DateTimeKind.Utc),
                root.GetProperty("submitted_diagnosis").GetString()!, root.GetProperty("submitted_action_taken").GetString()!, ids);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return null; }
    }
    internal static async Task<MaintenanceReviewDto?> ReadAsync(HotelOpsDbContext db, long user, long branch, long work, CancellationToken ct)
    {
        if (!await MaintenanceReviewAccess.AllowsAsync(db, user, branch, work, ct)) return null;
        var resolved = await db.MaintenanceWorks.AsNoTracking().Where(w => w.MaintenanceWorkId == work && w.Status == "COMPLETED")
            .Select(w => w.ResolvedAt).SingleOrDefaultAsync(ct);
        if (resolved == null) return null;
        var id = work.ToString(CultureInfo.InvariantCulture);
        var rows = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "maintenance_works" && a.EntityId == id
            && a.OccurredAt == resolved && a.Action == "UPDATE").ToArrayAsync(ct);
        var submissions = rows.Where(a => MaintenanceSubmissionSource.Parse(a) != null).ToArray();
        if (submissions.Length != 1) return null;
        var source = await MaintenanceSubmissionSource.ReadAsync(db, submissions[0].AuditId, ct);
        var review = Parse(submissions[0]);
        if (source == null || review == null) return null;
        if (review.ResultAttachmentIds.Count > 0)
        {
            var access = await IncidentReadAccess.ReadAsync(db, user, branch, ct, sourceRecord: true);
            if (!await access.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == source.IncidentId, ct)) return null;
        }
        return review;
    }
}
