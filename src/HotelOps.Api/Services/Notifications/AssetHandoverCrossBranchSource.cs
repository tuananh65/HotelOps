using HotelOps.Api.Data;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Assets;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal static partial class AssetHandoverSource
{
    private static async Task<Source?> ReadCrossBranchAsync(HotelOpsDbContext db, Source source, CancellationToken ct)
    {
        if (!await AssetTransferStorage.InstalledAsync(db, ct)) return null;
        var valid = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.asset_transfer_requests t WITH(HOLDLOCK)
            JOIN dbo.asset_handovers h WITH(HOLDLOCK) ON h.handover_id=t.handover_id
            JOIN dbo.asset_handover_versions v WITH(HOLDLOCK) ON v.handover_id=h.handover_id
            JOIN dbo.audit_logs a WITH(HOLDLOCK) ON a.audit_id=v.submission_audit_id
            WHERE h.handover_id={source.HandoverId} AND h.asset_id={source.AssetId}
              AND h.to_user_id={source.ReceiverId} AND h.to_department_id={source.DepartmentId}
              AND t.source_branch_id={source.BranchId} AND t.destination_branch_id={source.DestinationBranchId}
              AND a.entity_type='asset_handovers' AND a.entity_id=CONVERT(varchar(30),h.handover_id) AND a.action='CREATE'
              AND a.branch_id=t.source_branch_id AND a.actor_user_id=h.created_by AND a.occurred_at=h.created_at
              AND a.old_values IS NULL AND a.new_values=v.snapshot_json AND ISJSON(v.snapshot_json)=1
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.asset_id'))=h.asset_id
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.from_branch_id'))=t.source_branch_id
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.to_branch_id'))=t.destination_branch_id
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.to_user_id'))=h.to_user_id
              AND TRY_CONVERT(bigint,JSON_VALUE(v.snapshot_json,'$.to_department_id'))=h.to_department_id
              AND JSON_VALUE(v.snapshot_json,'$.condition_note')=h.condition_note
              AND (({source.EventKind}='ASSET_HANDOVER_SUBMITTED' AND a.audit_id={source.AuditId}
                    AND h.created_by={source.ActorId} AND h.created_at={source.At} AND h.handed_over_at={source.At}
                    AND h.handover_status IN('PENDING','ACCEPTED','REJECTED'))
                OR ({source.EventKind}='ASSET_HANDOVER_DECIDED' AND h.created_by={source.SenderId}
                    AND h.handover_status={source.Decision}
                    AND (h.handover_status='ACCEPTED' AND h.accepted_at={source.At}
                      OR h.handover_status='REJECTED' AND h.accepted_at IS NULL)))
            """).SingleAsync(ct);
        return valid == 1 ? source : null;
    }

    private static async Task<bool> CanReadCrossBranchAsync(HotelOpsDbContext db, long user, Source source, CancellationToken ct)
    {
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var permissions = new EfPermissionDataSource(db);
        if (!await permissions.IsActiveUserAsync(user, ct)) return false;
        var asset = await db.Assets.AsNoTracking().Where(a => a.AssetId == source.AssetId && a.Branch.IsActive
            && (a.CurrentRoom == null || a.CurrentRoom.BranchId == a.BranchId)
            && (a.CurrentArea == null || a.CurrentArea.BranchId == a.BranchId)
            && (a.CurrentDepartment == null || a.CurrentDepartment.BranchId == a.BranchId))
            .Select(a => new { a.BranchId }).SingleOrDefaultAsync(ct);
        if (asset == null) return false;
        var paths = await permissions.ReadPathsAsync(user, "ASSET_VIEW", now, ct);
        // The approved rule requires full current asset read, not a receipt-only exception.
        // Both original endpoints are checked as well before disclosing transfer history.
        foreach (var branch in new[] { source.BranchId, source.DestinationBranchId!.Value, asset.BranchId }.Distinct())
        {
            if (!await db.UserBranchAccesses.AnyAsync(a => a.UserId == user && a.BranchId == branch && a.Branch.IsActive
                    && a.RevokedAt == null && a.GrantedAt <= now, ct)
                || !await db.UserRoles.AnyAsync(r => r.UserId == user && r.IsActive && r.Role.IsActive
                    && new[] { "OPERATIONS_MANAGER", "HK_SUPERVISOR", "HK_STAFF", "MAINTENANCE_LEAD", "MAINTENANCE_STAFF", "STOREKEEPER", "DEPT_MANAGER", "OWNER" }.Contains(r.Role.RoleCode)
                    && (r.BranchId == null || r.BranchId == branch) && r.AssignedAt <= now && (r.ExpiresAt == null || r.ExpiresAt > now), ct)
                || !paths.Any(p => PermissionGrantRules.IsEligible(p, now)
                    && (PermissionGrantRules.EffectiveScope(p) == "GLOBAL" || p.BranchId == branch && PermissionGrantRules.EffectiveScope(p) == "BRANCH"))) return false;
        }
        if (source.EventKind == Kind || source.Cancelled)
            return await AssetHandoverAuthority.AllowsAsync(db, user, source.DestinationBranchId.Value, source.DepartmentId, true, ct);
        return true;
    }
}
