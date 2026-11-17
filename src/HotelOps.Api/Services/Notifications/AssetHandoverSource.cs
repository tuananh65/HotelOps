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
internal static partial class AssetHandoverSource
{
    internal const string Kind = "ASSET_HANDOVER_SUBMITTED";
    internal const string DecisionKind = "ASSET_HANDOVER_DECIDED";
    internal const string SourceType = "ASSET_HANDOVER";
    internal sealed record Source(long AuditId, long HandoverId, long AssetId,
        long BranchId, long DepartmentId, long ReceiverId, long ActorId, DateTime At,
        string EventKind = Kind, long? SenderId = null, string? Decision = null, long? DestinationBranchId = null, bool Cancelled = false)
    {
        internal long RecipientId => EventKind == DecisionKind && !Cancelled ? SenderId!.Value : ReceiverId;
        internal bool CrossBranch => DestinationBranchId is > 0 && DestinationBranchId != BranchId;
    }

    internal static Source? Parse(AuditLog audit)
    {
        if(audit.Action=="UPDATE")return ParseDecision(audit);
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
            var destination = value.TryGetProperty("to_branch_id", out var target) ? target.GetInt64() : audit.BranchId.Value;
            if (destination <= 0 || value.GetProperty("from_branch_id").GetInt64() != audit.BranchId) return null;
            return new(audit.AuditId, id, asset, audit.BranchId.Value, department,
                receiver, audit.ActorUserId.Value, audit.OccurredAt, DestinationBranchId: destination);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException)
        {
            return null;
        }
    }

    private static Source? ParseDecision(AuditLog audit)
    {
        if(audit.AuditId<=0||audit.EntityType!="asset_handovers"||audit.ActorUserId is not >0||audit.BranchId is not >0
            ||!long.TryParse(audit.EntityId,NumberStyles.None,CultureInfo.InvariantCulture,out var id)||id<=0)return null;
        try
        {
            using var before=JsonDocument.Parse(audit.OldValues??"null");
            using var after=JsonDocument.Parse(audit.NewValues??"null");var v=after.RootElement;
            var state=v.GetProperty("handover_status").GetString();var asset=v.GetProperty("asset_id").GetInt64();
            var department=v.GetProperty("to_department_id").GetInt64();var sender=v.GetProperty("sender_id").GetInt64();
            var receiver=v.GetProperty("to_user_id").GetInt64();
            var cancelled=v.TryGetProperty("decision",out var action)&&action.GetString()=="CANCEL";
            var destination=v.TryGetProperty("to_branch_id",out var target)?target.GetInt64():audit.BranchId.Value;
            if(before.RootElement.GetProperty("handover_status").GetString()!="PENDING"||state is not("ACCEPTED" or "REJECTED")
                ||v.GetProperty("notification_kind").GetString()!=DecisionKind||v.GetProperty("notification_version").GetInt32()!=1
                ||v.GetProperty("handover_id").GetInt64()!=id||v.GetProperty("branch_id").GetInt64()!=audit.BranchId
                ||(cancelled?sender:receiver)!=audit.ActorUserId||v.GetProperty("decided_at").GetDateTime()!=audit.OccurredAt
                ||asset<=0||department<=0||sender<=0||receiver<=0||destination<=0||cancelled&&state!="REJECTED"
                ||v.GetProperty("from_branch_id").GetInt64()!=audit.BranchId
                ||state=="ACCEPTED"&&v.GetProperty("accepted_at").GetDateTime()!=audit.OccurredAt
                ||state=="REJECTED"&&(v.GetProperty("accepted_at").ValueKind!=JsonValueKind.Null||v.GetProperty("reason").GetString()?.Trim().Length is not >=10))return null;
            return new(audit.AuditId,id,asset,audit.BranchId.Value,department,receiver,audit.ActorUserId.Value,
                audit.OccurredAt,DecisionKind,sender,state,destination,cancelled);
        }
        catch(Exception e)when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException){return null;}
    }

    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db, long auditId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Serializable handover notification source read required.");
        var audit = await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a => a.AuditId == auditId, ct);
        var source = audit == null ? null : Parse(audit);
        if (source == null) return null;
        if (source.CrossBranch) return await ReadCrossBranchAsync(db, source, ct);
        if(source.EventKind==DecisionKind)
            return await db.AssetHandovers.AsNoTracking().AnyAsync(h=>h.HandoverId==source.HandoverId
                &&h.AssetId==source.AssetId&&h.Asset.BranchId==source.BranchId&&h.ToUserId==source.ReceiverId
                &&h.CreatedBy==source.SenderId&&h.ToDepartmentId==source.DepartmentId&&h.HandoverStatus==source.Decision
                &&(source.Decision=="ACCEPTED"?h.AcceptedAt==source.At:h.AcceptedAt==null),ct)?source:null;
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
        if (user != source.RecipientId) return false;
        if (source.CrossBranch) return await CanReadCrossBranchAsync(db, user, source, ct);
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var permissions = new EfPermissionDataSource(db);
        if(source.EventKind==Kind)
        {
            if(!await new HotelOps.Api.Services.InternalRequests.InternalRequestProcessAuthority(db).InstalledAsync(ct)
                ||!await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==user&&r.IsActive&&r.Role.IsActive&&r.Role.RoleCode=="DEPT_MANAGER"
                    &&(r.BranchId==null||r.BranchId==source.BranchId)&&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now),ct)
                ||!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==source.DepartmentId&&d.BranchId==source.BranchId&&d.IsActive,ct)
                ||await db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS Value FROM dbo.department_management_access WITH(HOLDLOCK)
                    WHERE user_id={user} AND branch_id={source.BranchId} AND department_id={source.DepartmentId}
                      AND granted_at<={now} AND revoked_at IS NULL
                    """).SingleAsync(ct)!=1)return false;
        }
        if (!await permissions.IsActiveUserAsync(user, ct)
            || !await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==user&&r.IsActive&&r.Role.IsActive
                &&new[]{"OPERATIONS_MANAGER","HK_SUPERVISOR","HK_STAFF","MAINTENANCE_LEAD","MAINTENANCE_STAFF","STOREKEEPER","DEPT_MANAGER","OWNER"}.Contains(r.Role.RoleCode)
                &&(r.BranchId==null||r.BranchId==source.BranchId)&&r.AssignedAt<=now&&(r.ExpiresAt==null||r.ExpiresAt>now),ct)
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
