using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal static class MaintenanceClosureSource
{
    internal const string Kind = "MAINTENANCE_VERIFIED";
    internal const string SourceType = "MAINTENANCE_CLOSURE";
    internal sealed record Source(long AuditId, long WorkId, long IncidentId, long BranchId,
        long ActorId, long ReporterId, long TechnicianId, DateTime At, string Note);

    internal static Source? Parse(AuditLog audit)
    {
        if (audit.AuditId <= 0 || audit.EntityType != "maintenance_works" || audit.Action != "UPDATE"
            || audit.ActorUserId is not > 0 || audit.BranchId is not > 0
            || !long.TryParse(audit.EntityId, out var work) || work <= 0) return null;
        try
        {
            using var before = JsonDocument.Parse(audit.OldValues ?? "null");
            using var after = JsonDocument.Parse(audit.NewValues ?? "null");
            var b = before.RootElement; var a = after.RootElement;
            var technician = a.GetProperty("assignee_id").GetInt64();
            var reporter = a.GetProperty("closure_reporter_id").GetInt64();
            var incident = a.GetProperty("incident_id").GetInt64();
            var note = a.GetProperty("verification_note").GetString();
            if (b.GetProperty("status").GetString() != "COMPLETED" || a.GetProperty("status").GetString() != "CLOSED"
                || a.GetProperty("notification_kind").GetString() != Kind || a.GetProperty("notification_version").GetInt32() != 1
                || a.GetProperty("maintenance_work_id").GetInt64() != work || b.GetProperty("maintenance_work_id").GetInt64() != work
                || b.GetProperty("incident_id").GetInt64() != incident || incident <= 0 || reporter <= 0
                || b.GetProperty("assignee_id").GetInt64() != technician || technician <= 0 || technician == audit.ActorUserId
                || a.GetProperty("reviewed_by").GetInt64() != audit.ActorUserId || a.GetProperty("reviewed_at").GetDateTime() != audit.OccurredAt
                || string.IsNullOrWhiteSpace(note) || note != note.Trim() || note.Length < 10) return null;
            return new(audit.AuditId, work, incident, audit.BranchId.Value, audit.ActorUserId.Value, reporter, technician, audit.OccurredAt, note);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return null; }
    }

    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db, long auditId, CancellationToken ct)
    {
        var source = await ReadHistoricalAsync(db, auditId, ct);
        if (source == null) return null;
        return await db.Incidents.AsNoTracking().AnyAsync(i => i.IncidentId == source.IncidentId
            && i.Status == "CLOSED" && i.ClosedAt == source.At && i.ClosureNote == source.Note, ct) ? source : null;
    }

    // Historical closure proof remains bound to the closed work, audit and event.
    // A later Incident cycle must not alter that proof; recipients are authorized separately.
    internal static async Task<Source?> ReadHistoricalAsync(HotelOpsDbContext db, long auditId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Serializable notification source transaction required.");
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == auditId, ct);
        var source = audit == null ? null : Parse(audit);
        if (source == null) return null;
        if (!await db.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.MaintenanceWorkId == source.WorkId
            && w.IncidentId == source.IncidentId && w.Incident!.BranchId == source.BranchId
            && w.Status == "CLOSED" && w.Incident.ReportedBy == source.ReporterId
            && w.AssigneeId == source.TechnicianId && w.ReviewedBy == source.ActorId && w.ReviewedAt == source.At, ct)) return null;
        if (await db.IncidentEvents.AsNoTracking().CountAsync(e => e.IncidentId == source.IncidentId
            && e.EventType == "STATUS_CHANGED" && e.FromStatus == "RESOLVED" && e.ToStatus == "CLOSED"
            && e.ActorId == source.ActorId && e.OccurredAt == source.At && e.Note == source.Note, ct) != 1) return null;
        return source;
    }

    internal static async Task<string?> TargetAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
    {
        if (user == source.TechnicianId)
        {
            var work = await MaintenanceReadAccess.ReadAsync(db, user, source.BranchId, ct);
            if (await work.Apply(db.MaintenanceWorks.AsNoTracking()).AnyAsync(w => w.MaintenanceWorkId == source.WorkId && w.AssigneeId == user, ct))
                return $"/operations/maintenance/{source.WorkId}?action=A004A-036";
        }
        if (user == source.ReporterId)
        {
            var incident = await IncidentReadAccess.ReadAsync(db, user, source.BranchId, ct, sourceRecord: true);
            if (await incident.Apply(db.Incidents.AsNoTracking()).AnyAsync(i => i.IncidentId == source.IncidentId && i.ReportedBy == user, ct))
                return $"/operations/incidents/{source.IncidentId}?action=A004A-023";
        }
        return null;
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
        => await TargetAsync(db, user, source, ct) != null;
}
