using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// Event provenance only. Reading this event is not authorization to disclose
// either the handover receipt or the asset to its intended recipient.
internal static class AssetHandoverSource
{
    internal const string Kind = "ASSET_HANDOVER_SUBMITTED";
    internal const string SourceType = "ASSET_HANDOVER";
    internal sealed record Source(long AuditId, long HandoverId, long AssetId,
        long BranchId, long DepartmentId, long ReceiverId, long ActorId, DateTime At);

    internal static Source? Parse(AuditLog audit)
    {
        if (audit.AuditId <= 0 || audit.EntityType != "asset_handovers" || audit.Action != "CREATE"
            || audit.OldValues != null || audit.ActorUserId is not > 0 || audit.BranchId is not > 0
            || !long.TryParse(audit.EntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            return null;
        try
        {
            using var document = JsonDocument.Parse(audit.NewValues ?? "null");
            var value = document.RootElement;
            var asset = value.GetProperty("asset_id").GetInt64();
            var receiver = value.GetProperty("to_user_id").GetInt64();
            var department = value.GetProperty("to_department_id").GetInt64();
            if (asset <= 0 || receiver <= 0 || department <= 0
                || value.GetProperty("notification_kind").GetString() != Kind
                || value.GetProperty("notification_version").GetInt32() != 1
                || value.GetProperty("handover_id").GetInt64() != id
                || value.GetProperty("branch_id").GetInt64() != audit.BranchId
                || value.GetProperty("created_by").GetInt64() != audit.ActorUserId
                || value.GetProperty("created_at").GetDateTime() != audit.OccurredAt
                || value.GetProperty("handed_over_at").GetDateTime() != audit.OccurredAt
                || value.GetProperty("handover_status").GetString() != "PENDING"
                || value.GetProperty("accepted_at").ValueKind != JsonValueKind.Null)
                return null;
            return new(audit.AuditId, id, asset, audit.BranchId.Value, department,
                receiver, audit.ActorUserId.Value, audit.OccurredAt);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException)
        {
            return null;
        }
    }

    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db, long auditId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Serializable handover notification source read required.");
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == auditId, ct);
        var source = audit == null ? null : Parse(audit);
        if (source == null) return null;
        // Custody changes after acceptance are expected. Validate the original
        // receipt identity, not the asset's current custodian or department.
        return await db.AssetHandovers.AsNoTracking().AnyAsync(h => h.HandoverId == source.HandoverId
            && h.AssetId == source.AssetId && h.Asset.BranchId == source.BranchId
            && h.ToUserId == source.ReceiverId && h.ToDepartmentId == source.DepartmentId
            && h.ToDepartment != null && h.ToDepartment.BranchId == source.BranchId
            && h.CreatedBy == source.ActorId && h.CreatedAt == source.At && h.HandedOverAt == source.At
            && (h.HandoverStatus == "PENDING" || h.HandoverStatus == "ACCEPTED" || h.HandoverStatus == "REJECTED"), ct)
            ? source : null;
    }

    internal static async Task<bool> CanReadAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
    {
        if (user != source.ReceiverId) return false;
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var permissions = new EfPermissionDataSource(db);
        if (!await permissions.IsActiveUserAsync(user, ct)
            || !await db.UserBranchAccesses.AsNoTracking().AnyAsync(a => a.UserId == user
                && a.BranchId == source.BranchId && a.Branch.IsActive && a.RevokedAt == null && a.GrantedAt <= now, ct)
            || !await db.Assets.AsNoTracking().AnyAsync(a => a.AssetId == source.AssetId && a.BranchId == source.BranchId
                && (a.CurrentRoom == null || a.CurrentRoom.BranchId == a.BranchId)
                && (a.CurrentArea == null || a.CurrentArea.BranchId == a.BranchId)
                && (a.CurrentDepartment == null || a.CurrentDepartment.BranchId == a.BranchId), ct)) return false;
        // Match the existing full-asset branch read: receiver identity is never
        // resource ownership. No receipt-only OWN exception or implicit grant.
        return (await permissions.ReadPathsAsync(user, "ASSET_VIEW", now, ct)).Any(p =>
            p.PermissionCode == "ASSET_VIEW" && PermissionGrantRules.IsEligible(p, now)
            && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL"
                || p.BranchId == source.BranchId && PermissionGrantRules.EffectiveScope(p) == "BRANCH"));
    }
}
