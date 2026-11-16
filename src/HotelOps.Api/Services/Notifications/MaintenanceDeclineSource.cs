using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal static class MaintenanceDeclineSource
{
    internal const string Kind = "MAINTENANCE_DECLINED";
    internal const string SourceType = "MAINTENANCE_DECLINE";
    internal sealed record Source(long AuditId, long WorkId, long IncidentId, long BranchId, long ActorId, DateTime At);

    internal static Source? Parse(AuditLog audit)
    {
        if (audit.AuditId <= 0 || audit.EntityType != "maintenance_works" || audit.Action != "UPDATE"
            || audit.ActorUserId is not > 0 || audit.BranchId is not > 0 || !long.TryParse(audit.EntityId, out var work) || work <= 0) return null;
        try
        {
            using var before = JsonDocument.Parse(audit.OldValues ?? "null");
            using var after = JsonDocument.Parse(audit.NewValues ?? "null");
            var b = before.RootElement; var a = after.RootElement;
            var incident = a.GetProperty("incident_id").GetInt64();
            if (incident <= 0 || b.GetProperty("incident_id").GetInt64() != incident
                || b.GetProperty("maintenance_work_id").GetInt64() != work || a.GetProperty("maintenance_work_id").GetInt64() != work
                || b.GetProperty("status").GetString() != "PENDING" || a.GetProperty("status").GetString() != "PENDING"
                || b.GetProperty("assignee_id").GetInt64() != audit.ActorUserId
                || a.GetProperty("assignee_id").ValueKind != JsonValueKind.Null
                || a.GetProperty("previous_assignee_id").GetInt64() != audit.ActorUserId
                || a.GetProperty("notification_kind").GetString() != Kind || a.GetProperty("notification_version").GetInt32() != 1
                || a.GetProperty("reason").GetString()?.Trim().Length is not (>= 10 and <= 2000)) return null;
            return new(audit.AuditId, work, incident, audit.BranchId.Value, audit.ActorUserId.Value, audit.OccurredAt);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException) { return null; }
    }

    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db, long auditId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Serializable notification source transaction required.");
        if (!await MaintenanceDeclineStore.InstalledAsync(db, ct)) return null;
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == auditId, ct);
        var source = audit == null ? null : Parse(audit);
        if (source == null) return null;
        using var values = JsonDocument.Parse(audit!.NewValues!);
        var reason = values.RootElement.GetProperty("reason").GetString();
        if (!await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.maintenance_decline_events
            WHERE audit_id={auditId} AND maintenance_work_id={source.WorkId} AND incident_id={source.IncidentId}
              AND actor_id={source.ActorId} AND previous_assignee_id={source.ActorId} AND occurred_at={source.At} AND reason={reason}
            """).AnyAsync(ct)) return null;
        return await db.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.MaintenanceWorkId == source.WorkId
            && w.IncidentId == source.IncidentId && w.Incident!.BranchId == source.BranchId, ct) ? source : null;
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
    {
        var access = await MaintenanceReadAccess.ReadAsync(db, user, source.BranchId, ct);
        return access.Lead && await access.Apply(db.MaintenanceWorks.AsNoTracking()).AnyAsync(w => w.MaintenanceWorkId == source.WorkId, ct);
    }
}
