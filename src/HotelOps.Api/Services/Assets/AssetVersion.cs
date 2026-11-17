using HotelOps.Api.Data;
using HotelOps.Api.Services.Insights;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

internal static class AssetVersion
{
    internal static async Task<string> ReadAsync(HotelOpsDbContext db, long id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Asset version requires its transaction.");
        if (!await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.AssetVersion.V1' AND CONVERT(nvarchar(10),value)='1')
              AND EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.assets')
                AND name='row_version' AND system_type_id=189 AND max_length=8 AND is_nullable=0)
              THEN 1 ELSE 0 END AS Value
            """).AnyAsync(v => v == 1, ct))
            throw new InsightException(503, "ASSET_VERSION_STORAGE_UNAVAILABLE", "Asset rowversion migration required.");
        return await db.Database.SqlQuery<string>($"""
            SELECT CHAR(34)+CONVERT(varchar(16),row_version,2)+CHAR(34) AS Value
            FROM dbo.assets WITH(HOLDLOCK) WHERE asset_id={id}
            """).SingleOrDefaultAsync(ct) ?? throw new InsightException(404, "ASSET_UNAVAILABLE", "Asset unavailable.");
    }

    internal static async Task<byte[]> RequireAsync(HotelOpsDbContext db, long id, string? expected, CancellationToken ct)
    {
        if (expected == null) throw new InsightException(428, "VERSION_REQUIRED", "If-Match is required.");
        var current = await ReadAsync(db, id, ct);
        if (!string.Equals(expected, current, StringComparison.Ordinal))
            throw new InsightException(412, "MSG04", "Asset changed. Reload before saving.");
        return Convert.FromHexString(current[1..^1]);
    }
}
