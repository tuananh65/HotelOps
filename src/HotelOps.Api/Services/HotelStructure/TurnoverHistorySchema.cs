using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

// Read-only historical compatibility probes; no DDL or shared EF model changes.
internal static class TurnoverHistorySchema
{
    internal static async Task<IQueryable<Room>?> RoomsAsync(HotelOpsDbContext db, CancellationToken ct)
        => await RoomTypeDirectorySchema.ReadAsync(db, ct) switch
        {
            0 => db.Rooms,
            1 => db.Rooms.FromSqlRaw("SELECT r.* FROM dbo.rooms r INNER JOIN dbo.room_types t ON t.room_type_id=r.room_type_id WHERE t.branch_id IS NULL OR t.branch_id=r.branch_id"),
            _ => null
        };

    // 0 = unmodified legacy, 1 = installed, 2 = partial/incompatible. No startup DDL.
    public static Task<int> ChecklistStateAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQueryRaw<int>("""
        SELECT CASE WHEN COL_LENGTH(N'dbo.inspection_checklists',N'branch_id') IS NULL
         AND COL_LENGTH(N'dbo.inspection_checklists',N'row_version') IS NULL
         AND COL_LENGTH(N'dbo.inspection_checklist_items',N'criterion_group') IS NULL
         AND OBJECT_ID(N'dbo.inspection_checklist_room_types') IS NULL
         AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Checklists.V1') THEN 0
        WHEN COL_LENGTH(N'dbo.inspection_checklists',N'branch_id')=8 AND COL_LENGTH(N'dbo.inspection_checklist_items',N'criterion_group')=200
         AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.inspection_checklists') AND name=N'row_version' AND system_type_id=189 AND is_nullable=0)
         AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Checklists.V1' AND LEN(CONVERT(nvarchar(128),value))=64)
         AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN(N'FK_checklists_branch_v1',N'FK_checklist_types_checklist_v1',N'FK_checklist_types_type_v1') AND is_disabled=0 AND is_not_trusted=0)=3
         AND (SELECT COUNT(*) FROM sys.indexes WHERE name IN(N'IX_checklists_branch_v1',N'IX_checklist_types_reverse_v1',N'PK_checklist_room_types_v1') AND is_disabled=0)=3
         AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN(N'TR_checklists_scope_v1',N'TR_checklist_types_scope_v1',N'TR_checklist_items_immutable_v1',N'TR_checklist_versions_v1',N'TR_inspection_checklist_selection_v1',N'TR_room_types_checklist_v1') AND is_disabled=0)=6 THEN 1 ELSE 2 END AS [Value]
        """).SingleAsync(ct);


    public static async Task<bool> HousekeepingInstalledAsync(HotelOpsDbContext db, CancellationToken ct) => await db.Database.SqlQueryRaw<int>("""
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
        """).SingleAsync(ct) == 1 && await ChecklistStateAsync(db, ct) == 1;

}
