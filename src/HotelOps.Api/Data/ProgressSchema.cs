using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

public static class ProgressSchema
{
    public static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
        SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'HotelOps.Progress.V1')
        AND (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN('cleaning_item_definitions','cleaning_progress_versions','cleaning_task_items','task_progress_versions','task_completion_criteria','progress_evidence_links'))=6
        AND (SELECT COUNT(*) FROM sys.columns WHERE object_id IN(OBJECT_ID('dbo.cleaning_progress_versions'),OBJECT_ID('dbo.task_progress_versions')) AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)=2
        AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_cleaning_progress_items_v1','TR_task_progress_criteria_v1','TR_progress_evidence_v1','TR_progress_cleaning_parent_v1','TR_progress_task_parent_v1','TR_progress_cleaning_owner_v1','TR_progress_task_owner_v1') AND is_disabled=0)=7
        AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id IN(OBJECT_ID('dbo.cleaning_item_definitions'),OBJECT_ID('dbo.cleaning_progress_versions'),OBJECT_ID('dbo.cleaning_task_items'),OBJECT_ID('dbo.task_progress_versions'),OBJECT_ID('dbo.task_completion_criteria'),OBJECT_ID('dbo.progress_evidence_links')) AND is_disabled=0 AND is_not_trusted=0)=9
        THEN 1 ELSE 0 END AS [Value]
        """).SingleAsync(ct) == 1;
}
