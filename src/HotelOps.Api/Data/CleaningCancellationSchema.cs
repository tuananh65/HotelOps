using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Data;

internal static class CleaningCancellationSchema
{
    internal const string PackageHash="7D61CD67E15BA9710CB30612470E5AA674EB9EECEDF4568B1346168DF1B2F8EF";
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)
    {
        if(!await db.Database.SqlQuery<int>($"SELECT 1 AS [Value] FROM sys.extended_properties WHERE class=0 AND name='HotelOps.CleaningCancellation.V1' AND CONVERT(nvarchar(128),value)={PackageHash}").AnyAsync(ct)) return false;
        return await CleaningClosureSchema.InstalledAsync(db,ct)
            && await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_publications') AND name='CK_dev3_cancellation_event' AND is_disabled=0 AND is_not_trusted=0").SingleAsync(ct)==1;
    }
}
