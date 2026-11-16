using HotelOps.Api.Data;
using HotelOps.Api.Services.Dev2Authorization;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceExecutionVersion
{
    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct)
        => await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.MaintenanceExecutionVersion.V1')
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.maintenance_works')
                AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)
            THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static async Task<string?> TagAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        if (!await InstalledAsync(db, ct)) return null;
        return await db.Database.SqlQuery<string>($"""
            SELECT CHAR(34)+CONVERT(varchar(16),row_version,2)+CHAR(34) AS Value
            FROM dbo.maintenance_works WITH(HOLDLOCK) WHERE maintenance_work_id={id}
            """).SingleOrDefaultAsync(ct);
    }

    internal static async Task RequireAsync(HotelOpsDbContext db, long id, string? tag, CancellationToken ct)
    {
        var current = await TagAsync(db, id, ct) ?? throw new Dev2AccessDenied(503);
        if (tag != current) throw new Dev2AccessDenied(412);
    }
}
