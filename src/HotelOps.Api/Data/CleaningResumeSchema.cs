using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

internal static class CleaningResumeSchema
{
    internal const string PackageHash = "56B2F290C05D30F1E4DE0C01AC9E01FFA582455A7A8F6AD8EFBBDC8F0CDD5CD9";

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        if (!await db.Database.SqlQuery<int>($"""
            SELECT 1 AS [Value] FROM sys.extended_properties WHERE class=0
            AND name='HotelOps.CleaningResume.V1' AND CONVERT(nvarchar(128),value)={PackageHash}
            """).AnyAsync(ct)) return false;
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.triggers
            WHERE object_id=OBJECT_ID('dbo.TR_fs014_blocker_version')
            AND parent_id=OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions') AND is_disabled=0
            """).SingleAsync(ct) != 1) return false;
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.foreign_keys
            WHERE parent_object_id=OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions')
            AND is_disabled=0 AND is_not_trusted=0
            """).SingleAsync(ct) != 2) return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.check_constraints
            WHERE parent_object_id=OBJECT_ID('dbo.fs014_cleaning_blocker_resolutions')
            AND is_disabled=0 AND is_not_trusted=0
            """).SingleAsync(ct) == 2;
    }
}
