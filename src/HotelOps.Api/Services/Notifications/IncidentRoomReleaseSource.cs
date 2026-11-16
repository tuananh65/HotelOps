using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal static class IncidentRoomReleaseSource
{
    internal const string Kind = "INCIDENT_ROOM_RELEASE", SourceType = "ROOM_RELEASE";
    internal sealed record Source(long AuditId, long RoomId, long WorkId, long IncidentId, long BranchId,
        long ActorId, DateTime At, string BlockIds, string Readiness);
    internal static Source? Parse(AuditLog audit)
    {
        if (audit.AuditId <= 0 || audit.EntityType != "rooms" || audit.Action != "UPDATE"
            || audit.ActorUserId is not > 0 || audit.BranchId is not > 0
            || !long.TryParse(audit.EntityId, out var room) || room <= 0) return null;
        try
        {
            using var before = JsonDocument.Parse(audit.OldValues ?? "null");
            using var after = JsonDocument.Parse(audit.NewValues ?? "null");
            var b = before.RootElement; var a = after.RootElement;
            var ids = a.GetProperty("released_block_ids").EnumerateArray().Select(x => x.GetInt64()).Order().ToArray();
            var work = a.GetProperty("maintenance_work_id").GetInt64();
            var incident = a.GetProperty("incident_id").GetInt64();
            var readiness = a.GetProperty("effective_readiness").GetString();
            if (a.GetProperty("notification_kind").GetString() != Kind || a.GetProperty("notification_version").GetInt32() != 1
                || b.GetProperty("maintenance_work_id").GetInt64() != work || work <= 0 || incident <= 0
                || b.GetProperty("released_block_ids").GetArrayLength() != 0
                || ids.Length == 0 || ids.Any(x => x <= 0) || ids.Distinct().Count() != ids.Length
                || readiness is not ("INACTIVE" or "BLOCKED" or "READY" or "REWORK_REQUIRED" or "PENDING_CLEANING"
                    or "PENDING_INSPECTION" or "CLEANING_IN_PROGRESS" or "UNKNOWN")) return null;
            return new(audit.AuditId, room, work, incident, audit.BranchId.Value, audit.ActorUserId.Value,
                audit.OccurredAt, string.Join(",", ids.Select(x => x.ToString(CultureInfo.InvariantCulture))), readiness);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return null; }
    }

    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Serializable room notification read required.");
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == id, ct);
        var source = audit == null ? null : Parse(audit);
        if (source == null) return null;
        var blocks = source.BlockIds.Split(',').Select(x => long.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        if (await db.RoomBlocks.AsNoTracking().CountAsync(b => blocks.Contains(b.BlockId) && b.RoomId == source.RoomId
            && b.IncidentId == source.IncidentId && b.BlockedAt <= source.At && b.ReleasedAt == source.At
            && b.ReleasedBy == source.ActorId && b.Room.BranchId == source.BranchId, ct) != blocks.Length) return null;
        // Historical closure evidence remains valid after a subsequent Reopen. Never infer
        // closure merely from a mutable status or a stored notification URL.
        var workId = source.WorkId.ToString(CultureInfo.InvariantCulture);
        var closures = await db.AuditLogs.AsNoTracking().Where(a => a.EntityType == "maintenance_works"
            && a.EntityId == workId && a.ActorUserId == source.ActorId && a.OccurredAt == source.At
            && a.BranchId == source.BranchId).ToArrayAsync(ct);
        if (closures.Select(MaintenanceClosureSource.Parse).Count(c => c != null && c.IncidentId == source.IncidentId) != 1
            || await db.IncidentEvents.AsNoTracking().CountAsync(e => e.IncidentId == source.IncidentId
                && e.EventType == "STATUS_CHANGED" && e.FromStatus == "RESOLVED" && e.ToStatus == "CLOSED"
                && e.ActorId == source.ActorId && e.OccurredAt == source.At, ct) != 1) return null;
        return source;
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var data = new EfPermissionDataSource(db);
        if (!await data.IsActiveUserAsync(user, ct) || !await data.IsActiveBranchAsync(source.BranchId, ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user && a.BranchId == source.BranchId
                && a.RevokedAt == null && a.GrantedAt <= now, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r => r.UserId == user && r.BranchId == source.BranchId
                && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "HK_SUPERVISOR" && r.AssignedAt <= now
                && (r.ExpiresAt == null || r.ExpiresAt > now), ct)) return false;
        var rooms = await RoomTypeSchema.RoomsAsync(db, ct);
        if (rooms == null || !await rooms.AsNoTracking().AnyAsync(r => r.RoomId == source.RoomId && r.BranchId == source.BranchId
            && r.Floor.BranchId == r.BranchId && (r.Area == null || r.Area.BranchId == r.BranchId
                && (r.Area.FloorId == null || r.Area.FloorId == r.FloorId)), ct)) return false;
        return AllowsRoomGrant(await data.ReadPathsAsync(user, "ROOM_VIEW", now, ct), source.BranchId, now);
    }

    internal static bool AllowsRoomGrant(IEnumerable<PermissionGrantPath> paths, long branch, DateTime now)
        => paths.Any(g => g.PermissionCode == "ROOM_VIEW" && PermissionGrantRules.IsEligible(g, now)
            && (PermissionGrantRules.EffectiveScope(g) == "GLOBAL"
                || g.BranchId == branch && PermissionGrantRules.EffectiveScope(g) == "BRANCH"));
}
