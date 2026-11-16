using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

internal static class CleaningClosureSchema
{
    internal const string PackageHash = "B1EFE4DFD1D09FC3C6E5EF8B872A277B3392037C16207E45590D783E15B7B7E7";

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        if (!await db.Database.SqlQuery<int>($"""
            SELECT 1 AS [Value] FROM sys.extended_properties WHERE class=0
            AND name='HotelOps.CleaningClosure.V1' AND CONVERT(nvarchar(128),value)={PackageHash}
            """).AnyAsync(ct)) return false;
        if (!await CleaningStartSchema.InstalledAsync(db, ct) || !await CleaningResumeSchema.InstalledAsync(db, ct)
            || !await Services.HotelStructure.RoomTypeCreationService.CleanerAttributionInstalled(db, ct)) return false;
        if (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.triggers WHERE name IN
              ('TR_fs009_publication_immutable','TR_fs009_operation_immutable','TR_dev3_closure_publication_source')
              AND is_disabled=0
            """).SingleAsync(ct) != 3) return false;
        return await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications')
              AND name IN('UX_fs009_publication_operation','UX_fs015_assignment_publication','UX_fs015_occurrence')
              AND is_unique=1 AND is_disabled=0
            """).SingleAsync(ct) == 3;
    }
}
