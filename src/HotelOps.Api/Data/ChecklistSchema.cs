using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public static class ChecklistSchema
{
    // 0 = unmodified legacy, 1 = installed, 2 = partial/incompatible. No startup DDL.
    public static Task<int> StateAsync(HotelOpsDbContext db, CancellationToken ct) => db.Database.SqlQueryRaw<int>("""
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
}
