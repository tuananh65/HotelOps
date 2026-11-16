using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal static class MaintenanceReturnSource
{
    internal const string Kind = "MAINTENANCE_RETURNED";
    internal const string SourceType = "MAINTENANCE_RETURN";
    internal sealed record Source(long AuditId, long WorkId, long IncidentId, long BranchId,
        long ActorId, long RecipientId, DateTime At, string Reason);

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
            var recipient = a.GetProperty("assignee_id").GetInt64();
            var incident = a.GetProperty("incident_id").GetInt64();
            var reason = a.GetProperty("return_reason").GetString();
            if (b.GetProperty("status").GetString() != "COMPLETED" || a.GetProperty("status").GetString() != "IN_PROGRESS"
                || a.GetProperty("notification_kind").GetString() != Kind || a.GetProperty("notification_version").GetInt32() != 1
                || a.GetProperty("maintenance_work_id").GetInt64() != work
                || b.GetProperty("maintenance_work_id").GetInt64() != work
                || b.GetProperty("incident_id").GetInt64() != incident || incident <= 0
                || b.GetProperty("assignee_id").GetInt64() != recipient || recipient <= 0 || recipient == audit.ActorUserId
                || a.GetProperty("resolved_at").ValueKind != JsonValueKind.Null
                || string.IsNullOrWhiteSpace(reason) || reason != reason.Trim() || reason.Length < 10) return null;
            return new(audit.AuditId, work, incident, audit.BranchId.Value, audit.ActorUserId.Value, recipient, audit.OccurredAt, reason);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return null; }
    }

    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db, long auditId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Serializable notification source transaction required.");
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == auditId, ct);
        var source = audit == null ? null : Parse(audit);
        if (source == null) return null;
        if (!await db.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.MaintenanceWorkId == source.WorkId
            && w.IncidentId == source.IncidentId && w.Incident!.BranchId == source.BranchId
            && (w.Status == "IN_PROGRESS" || w.Status == "ON_HOLD") && w.Incident.Status == "IN_PROGRESS"
            && w.AssigneeId == source.RecipientId, ct)) return null;
        var events = await db.IncidentEvents.AsNoTracking().Where(e => e.IncidentId == source.IncidentId
            && e.EventType == "STATUS_CHANGED" && e.FromStatus == "RESOLVED" && e.ToStatus == "IN_PROGRESS"
            && e.ActorId == source.ActorId && e.OccurredAt == source.At && e.Note == source.Reason)
            .Select(e => e.EventId).Take(2).ToArrayAsync(ct);
        if (events.Length != 1) return null;
        var eventId = events[0];
        // A subsequent result submission or return supersedes this actionable notice.
        if (await db.IncidentEvents.AsNoTracking().AnyAsync(e => e.IncidentId == source.IncidentId
            && e.EventType == "STATUS_CHANGED" && e.EventId > eventId
            && (e.ToStatus == "RESOLVED" || e.ToStatus == "CLOSED" || e.ToStatus == "CANCELLED"), ct)) return null;
        return source;
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
    {
        if (user != source.RecipientId) return false;
        var scope = await MaintenanceReadAccess.ReadAsync(db, user, source.BranchId, ct);
        return await scope.Apply(db.MaintenanceWorks.AsNoTracking()).AnyAsync(w => w.MaintenanceWorkId == source.WorkId
            && w.AssigneeId == user, ct);
    }
}
