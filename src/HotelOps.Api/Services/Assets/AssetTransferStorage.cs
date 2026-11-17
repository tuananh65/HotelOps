using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

internal static class AssetTransferStorage
{
    // JSON is passed as a SQL parameter to existing raw readers; never concatenate IDs
    // into SQL and never reference the additive table on an uninstalled database.
    internal static async Task<string> OccurrenceScopesAsync(HotelOpsDbContext db, long? branch, CancellationToken ct)
    {
        if (!await InstalledAsync(db, ct)) return "[]";
        var rows = await db.AssetWorkSourceScopes.AsNoTracking().Where(s => (branch == null || s.BranchId == branch) && s.Run != null
            && s.Run.Schedule.AssetId == s.AssetId).Select(s => new { run = s.RunId, asset = s.AssetId, branch = s.BranchId }).ToArrayAsync(ct);
        return System.Text.Json.JsonSerializer.Serialize(rows);
    }

    internal static Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
          AND name='HotelOps.AssetTransfer.V1' AND CONVERT(nvarchar(10),value)='1')
          AND COL_LENGTH('dbo.asset_work_source_scopes','scope_id')=8
          AND COL_LENGTH('dbo.asset_transfer_requests','handover_id')=8
          AND COL_LENGTH('dbo.asset_schedule_source_scopes','schedule_id')=8
          AND COL_LENGTH('dbo.assets','row_version')=8
          AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_asset_handover_versions_immutable') AND is_disabled=0)
          AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.asset_handovers')
            AND name='UX_asset_handover_pending_v1' AND is_unique=1 AND is_disabled=0 AND has_filter=1)
          AND OBJECT_ID('dbo.AssetWorkLocationEligibleV1','FN') IS NOT NULL
          AND (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND name IN
            ('TR_asset_transfer_requests_immutable','TR_asset_work_source_scopes_immutable','TR_incident_original_asset_scope',
             'TR_asset_schedule_source_scopes_immutable','TR_asset_schedule_original_target','TR_asset_run_original_target',
             'TR_location_incidents_v1','TR_location_preventive_maintenance_runs_v1','TR_location_maintenance_works_v1'))=9
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.asset_work_source_scopes')
            AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=6
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.asset_schedule_source_scopes')
            AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=5
          AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.asset_transfer_requests')
            AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=7
          AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.asset_work_source_scopes')
            AND name IN('UX_asset_work_scope_incident','UX_asset_work_scope_run') AND is_unique=1 AND is_disabled=0 AND has_filter=1)=2
          THEN 1 ELSE 0 END AS Value
        """).AnyAsync(v => v == 1, ct);
}
