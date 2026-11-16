using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

internal static class CleaningCancellationReadinessSchema
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
    {
        const string hash = "7E54344D7D50C1322BD8265860C1299F675F3055FE33D5720A4834F299E5E6D9";
        if (!await db.Database.SqlQuery<int>($"SELECT 1 AS [Value] FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningCancellationReadiness.V1' AND CONVERT(nvarchar(128),value)={hash}").AnyAsync(ct)) return false;
        return await CleaningCancellationSchema.InstalledAsync(db, ct)
            && await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.room_readiness_events') AND name='CK_dev3_readiness_start' AND is_disabled=0 AND is_not_trusted=0").SingleAsync(ct) == 1;
    }
}
