using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

internal static class IncidentAssetFollowupSource
{
    internal const string Kind = "INCIDENT_ASSET_FOLLOWUP", SourceType = "ASSET_FOLLOWUP";
    internal sealed record Source(long AuditId, long AssetId, long WorkId, long IncidentId, long BranchId,
        long ActorId, DateTime At, string Condition);
    internal static Source? Parse(AuditLog audit)
    {
        var closure = MaintenanceClosureSource.Parse(audit);
        if (closure == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(audit.NewValues!);
            var id = doc.RootElement.GetProperty("closure_asset_id").GetInt64();
            var condition = doc.RootElement.GetProperty("closure_asset_condition").GetString();
            return id > 0 && condition is "DAMAGED" or "UNDER_REPAIR"
                ? new(audit.AuditId, id, closure.WorkId, closure.IncidentId, closure.BranchId, closure.ActorId, closure.At, condition) : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return null; }
    }
    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Serializable asset follow-up read required.");
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == id, ct);
        var source = audit == null ? null : Parse(audit);
        if (source == null || !await db.Incidents.AsNoTracking().AnyAsync(i => i.IncidentId == source.IncidentId
            && i.AssetId == source.AssetId && i.BranchId == source.BranchId, ct)
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
                && r.IsActive && r.Role.IsActive && r.Role.RoleCode == "OPERATIONS_MANAGER" && r.AssignedAt <= now
                && (r.ExpiresAt == null || r.ExpiresAt > now), ct)) return false;
        var asset = await db.Assets.AsNoTracking().Where(a => a.AssetId == source.AssetId && a.BranchId == source.BranchId
            && (a.CurrentRoom == null || a.CurrentRoom.BranchId == a.BranchId)
            && (a.CurrentArea == null || a.CurrentArea.BranchId == a.BranchId)
            && (a.CurrentDepartment == null || a.CurrentDepartment.BranchId == a.BranchId))
            .Select(a => new { a.CustodianUserId }).SingleOrDefaultAsync(ct);
        return asset != null && Allows(await data.ReadPathsAsync(user, "ASSET_VIEW", now, ct), source.BranchId,
            asset.CustodianUserId == user, now);
    }
    internal static bool Allows(IEnumerable<PermissionGrantPath> paths, long branch, bool own, DateTime now)
        => paths.Any(g => g.PermissionCode == "ASSET_VIEW" && PermissionGrantRules.IsEligible(g, now)
            && (PermissionGrantRules.EffectiveScope(g) == "GLOBAL" || g.BranchId == branch
                && (PermissionGrantRules.EffectiveScope(g) == "BRANCH" || own && PermissionGrantRules.EffectiveScope(g) == "OWN")));
}
