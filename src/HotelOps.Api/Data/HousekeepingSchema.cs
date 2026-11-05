using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public static class HousekeepingSchema
{
    public static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) => await db.Database.SqlQueryRaw<int>("""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Housekeeping.V1' AND LEN(CONVERT(nvarchar(128),value))=64)
        AND (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID(N'dbo.cleaning_tasks'),OBJECT_ID(N'dbo.room_inspections'),OBJECT_ID(N'dbo.cleaning_reworks')) AND name='row_version' AND system_type_id=189 AND is_nullable=0)=3
        AND COL_LENGTH(N'dbo.inspection_defects',N'room_location')=500
        AND COL_LENGTH(N'dbo.cleaning_tasks',N'workflow_status')=30 AND COL_LENGTH(N'dbo.room_inspections',N'workflow_status')=30
        AND COL_LENGTH(N'dbo.cleaning_reworks',N'workflow_status')=30 AND COL_LENGTH(N'dbo.room_inspections',N'due_at')=8
        AND COL_LENGTH(N'dbo.cleaning_reworks',N'due_at')=8
        AND (SELECT COUNT(*) FROM sys.check_constraints WHERE name IN('CK_cleaning_workflow_v1','CK_inspection_workflow_v1','CK_rework_workflow_v1') AND is_disabled=0 AND is_not_trusted=0)=3
        AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_rework_defects_rework_v1','FK_rework_defects_defect_v1','FK_defect_attachment_defect_v1','FK_defect_attachment_file_v1','FK_result_attachment_result_v1','FK_result_attachment_file_v1') AND is_disabled=0 AND is_not_trusted=0)=6
        AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_hk_inspections_v1','TR_hk_results_v1','TR_hk_defects_v1','TR_hk_rework_links_v1','TR_hk_defect_files_v1','TR_hk_result_files_v1','TR_hk_used_criteria_v1','TR_hk_used_version_v1') AND is_disabled=0)=8
        THEN 1 ELSE 0 END AS [Value]
        """).SingleAsync(ct) == 1 && await ChecklistSchema.StateAsync(db, ct) == 1;
}
