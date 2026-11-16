using HotelOps.Api.Data;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceProgressStore
{
    internal sealed class Row { public long LogId { get; set; } public int ProgressPercent { get; set; } }
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.MaintenanceProgress.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.maintenance_log_progress','progress_percent')=4
              AND EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.maintenance_log_progress')
                AND name='FK_maintenance_log_progress_log' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.maintenance_log_progress')
                AND name='CK_maintenance_log_progress_percent' AND is_disabled=0 AND is_not_trusted=0)
              AND EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID('dbo.TR_maintenance_log_progress_immutable') AND is_disabled=0)
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static async Task WriteAsync(HotelOpsDbContext db, long log, int percent, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || !await InstalledAsync(db, ct)) throw new Dev2AccessDenied(503);
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.maintenance_log_progress(log_id,progress_percent) VALUES({log},{percent})", ct);
    }

    // Caller has already scoped the owning work; do not expose an independent public read.
    internal static Task<Row[]> ReadAsync(HotelOpsDbContext db, long work, CancellationToken ct) => db.Database.SqlQuery<Row>($"""
        SELECT p.log_id AS LogId,p.progress_percent AS ProgressPercent
        FROM dbo.maintenance_log_progress p JOIN dbo.maintenance_work_logs l ON l.log_id=p.log_id
        WHERE l.maintenance_work_id={work}
        """).ToArrayAsync(ct);
}
