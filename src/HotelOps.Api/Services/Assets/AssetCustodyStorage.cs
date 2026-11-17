using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetCustodyWorkflow
{
    private async Task RequireStorageAsync(CancellationToken ct)
    {
        var ready=await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.AssetCustody.V1' AND CONVERT(nvarchar(10),value)='1')
              AND COL_LENGTH('dbo.asset_handover_versions','handover_id')=8
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_asset_handover_versions_immutable') AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.asset_handovers')
                AND name='UX_asset_handover_pending_v1' AND is_unique=1 AND is_disabled=0)
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.asset_handover_versions')
                AND name IN('FK_asset_handover_version_receipt','FK_asset_handover_version_previous','FK_asset_handover_version_audit')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=3
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct);
        if(ready!=1)throw new InsightException(503,"ASSET_CUSTODY_STORAGE_REQUIRED","Install the guarded custody version schema before using this workflow.");
    }
}
