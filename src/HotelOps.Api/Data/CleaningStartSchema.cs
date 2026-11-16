using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

// Start is independent of the legacy FS013 reassignment procedure and notification role.
// This capability identifies the reviewed execution package; it grants no actor permission.
internal static class CleaningStartSchema
{
    internal const string PackageHash = "F7B07D054E6A9D666EEF5D16CEE2E2F2F2CF16BACFC8E15823989ECF7B3D3170";

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        // Separate metadata probes avoid a large cross-catalog optimizer search during cold compilation.
        if (!await db.Database.SqlQuery<int>($"""
            SELECT 1 AS [Value] FROM sys.extended_properties
            WHERE class=0 AND name='HotelOps.CleaningExecution.V1'
              AND CONVERT(nvarchar(128),value)={PackageHash}
            """).AnyAsync(ct)) return false;
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo')
              AND name IN('workdays','work_intervals','room_cleaning_cycle_state','room_readiness_events')
            """).SingleAsync(ct) != 4) return false;
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.check_constraints WHERE name IN
              ('CK_dev3_workday_state','CK_dev3_workday_time','CK_dev3_interval_time','CK_dev3_readiness_start')
              AND is_disabled=0 AND is_not_trusted=0
            """).SingleAsync(ct) != 4) return false;
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE is_unique=1 AND is_disabled=0 AND has_filter=1
              AND ((object_id=OBJECT_ID('dbo.workdays') AND name='UX_workdays_one_working')
                OR (object_id=OBJECT_ID('dbo.work_intervals') AND name='UX_work_intervals_one_open'))
            """).SingleAsync(ct) != 2) return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.foreign_keys WHERE name IN
              ('FK_work_intervals_workday','FK_room_cycle_task_v1','FK_readiness_task_room_v1')
              AND is_disabled=0 AND is_not_trusted=0
            """).SingleAsync(ct) == 3;
    }
}