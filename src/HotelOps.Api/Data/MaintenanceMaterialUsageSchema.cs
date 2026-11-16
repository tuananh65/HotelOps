using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

internal static class MaintenanceMaterialUsageSchema
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN OBJECT_ID('dbo.maintenance_material_usages','U') IS NOT NULL
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.maintenance_material_usages')
                AND name IN('FK_material_usages_work','FK_material_usages_issue_line','FK_material_usages_item',
                  'FK_material_usages_base_uom','FK_material_usages_uom','FK_material_usages_log_work')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=6
              AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.maintenance_material_usages')
                AND name IN('CK_material_usages_quantity','CK_material_usages_base_factor') AND is_disabled=0 AND is_not_trusted=0)=2
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.maintenance_material_usages')
                AND name='UQ_material_usages_log_issue' AND is_unique=1 AND is_disabled=0)
              AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.maintenance_work_logs')
                AND name='UQ_maintenance_logs_work' AND is_unique=1 AND is_disabled=0)
              AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.maintenance_material_usages')
                AND name IN('used_qty','used_base_qty') AND TYPE_NAME(system_type_id)='decimal' AND precision=18 AND scale=3 AND is_nullable=0)=2
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.maintenance_material_usages')
                AND name='conversion_factor' AND TYPE_NAME(system_type_id)='decimal' AND precision=18 AND scale=6 AND is_nullable=0)
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;
}
