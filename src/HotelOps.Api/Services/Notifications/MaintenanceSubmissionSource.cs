using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal static class MaintenanceSubmissionSource
{
    internal const string Kind = "MAINTENANCE_SUBMITTED";
    internal const string SourceType = "MAINTENANCE_SUBMISSION";
    internal sealed record Source(long AuditId, long WorkId, long IncidentId, long BranchId, long ActorId, DateTime At);

    internal static Source? Parse(AuditLog audit)
    {
        if (audit.EntityType != "maintenance_works" || audit.Action != "UPDATE"
            || audit.ActorUserId is not > 0 || audit.BranchId is not > 0
            || !long.TryParse(audit.EntityId, out var work) || work <= 0) return null;
        try
        {
            using var before = JsonDocument.Parse(audit.OldValues ?? "null");
            using var after = JsonDocument.Parse(audit.NewValues ?? "null");
            var b = before.RootElement; var a = after.RootElement;
            if (b.GetProperty("status").GetString() != "IN_PROGRESS" || a.GetProperty("status").GetString() != "COMPLETED"
                || a.GetProperty("notification_kind").GetString() != Kind || a.GetProperty("notification_version").GetInt32() != 1
                || a.GetProperty("maintenance_work_id").GetInt64() != work
                || a.GetProperty("assignee_id").GetInt64() != audit.ActorUserId
                || a.GetProperty("resolved_at").GetDateTime() != audit.OccurredAt
                || string.IsNullOrWhiteSpace(a.GetProperty("submitted_diagnosis").GetString())
                || string.IsNullOrWhiteSpace(a.GetProperty("submitted_action_taken").GetString())) return null;
            var incident = a.GetProperty("incident_id").GetInt64();
            return incident > 0 ? new(audit.AuditId, work, incident, audit.BranchId.Value, audit.ActorUserId.Value, audit.OccurredAt) : null;
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
            && w.Status == "COMPLETED" && w.Incident.Status == "RESOLVED" && w.ResolvedAt == source.At
            && w.Incident.ResolvedAt == source.At && w.AssigneeId == source.ActorId, ct)) return null;
        if (!await db.WorkIntervals.AsNoTracking().AnyAsync(i => i.WorkItemKind == "MAINTENANCE"
            && i.WorkItemId == source.WorkId && i.UserId == source.ActorId && i.BranchId == source.BranchId
            && i.ClosedAt == source.At && i.EffectiveEndedAt == source.At, ct)) return null;
        return source;
    }

    internal static async Task<bool> CanReviewAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
    {
        if (user == source.ActorId || await MaintenanceReviewSeparation.PerformedAsync(db, user, source.WorkId, ct)) return false;
        return await MaintenanceReviewAccess.AllowsAsync(db, user, source.BranchId, source.WorkId, ct);
    }
}
