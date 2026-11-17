using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

internal static class BranchDeactivationCoordination
{
    internal static async Task<bool> ReadyAsync(HotelOpsDbContext db,CancellationToken ct)
        => await LocationGuardSchema.ReadyAsync(db,ct) && await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.BranchDeactivation.V1' AND CONVERT(nvarchar(10),value)='1')
              AND (SELECT COUNT(*) FROM sys.triggers WHERE is_disabled=0 AND is_instead_of_trigger=0
                AND name IN('TR_branch_internal_requests_v1','TR_branch_material_requests_v1',
                  'TR_branch_material_replenishments_v1','TR_branch_stock_movements_v1',
                  'TR_branch_work_intervals_v1','TR_branch_cleaning_tasks_v1',
                  'TR_branch_cleaning_reworks_v1','TR_branch_room_inspections_v1',
                  'TR_branch_occurrence_details_v1','TR_branch_maintenance_works_v1',
                  'TR_branch_warehouses_v1'))=11 THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct)==1;
}
