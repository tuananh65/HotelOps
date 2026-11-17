using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Inventory;

internal static class StocktakeSeparation
{
    internal static async Task<bool> PerformedAsync(HotelOpsDbContext db, long stocktake, long actor, CancellationToken ct)
    {
        if (await db.Stocktakes.AsNoTracking().AnyAsync(s => s.StocktakeId == stocktake &&
            (s.CountedBy == actor || s.StocktakeLines.Any(l => l.CountedBy == actor)), ct)) return true;
        // Recount begins with Return; current line attribution also protects counts carried into that cycle.
        return await db.Database.SqlQuery<int>($$"""
            SELECT COUNT(*) AS Value FROM dbo.audit_logs a
            CROSS APPLY(SELECT CASE WHEN ISJSON(a.new_values)=1 THEN a.new_values ELSE N'{}' END AS payload) j
            WHERE a.entity_type='stocktakes' AND a.entity_id=CONVERT(nvarchar(30),{{stocktake}}) AND a.actor_user_id={{actor}}
              AND JSON_VALUE(j.payload,'$.businessEvent') IN ('COUNTS_SAVED','submit','reconcile')
              AND a.audit_id>COALESCE((SELECT MAX(r.audit_id) FROM dbo.audit_logs r
                  WHERE r.entity_type='stocktakes' AND r.entity_id=a.entity_id
                    AND JSON_VALUE(CASE WHEN ISJSON(r.new_values)=1 THEN r.new_values ELSE N'{}' END,'$.businessEvent')='return'),0)
            """).SingleAsync(ct) > 0;
    }
}
