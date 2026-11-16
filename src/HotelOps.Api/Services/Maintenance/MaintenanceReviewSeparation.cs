using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Maintenance;

internal static class MaintenanceReviewSeparation
{
    internal static async Task<bool> PerformedAsync(HotelOpsDbContext db, long user, long work, CancellationToken ct)
    {
        if (await db.MaintenanceWorks.AsNoTracking().AnyAsync(w => w.MaintenanceWorkId == work && w.AssigneeId == user, ct)
            || await db.WorkIntervals.AsNoTracking().AnyAsync(i => i.UserId == user
                && i.WorkItemKind == "MAINTENANCE" && i.WorkItemId == work, ct)) return true;
        // A reassignment must not erase a prior submitter's segregation-of-duties evidence.
        // This includes historical submissions that predate the notification intent marker.
        return await db.Database.SqlQuery<int>($"""
            SELECT 1 AS Value FROM dbo.audit_logs
            WHERE entity_type='maintenance_works' AND action='UPDATE' AND actor_user_id={user}
              AND TRY_CONVERT(bigint,entity_id)={work}
              AND JSON_VALUE(CASE WHEN ISJSON(old_values)=1 THEN old_values ELSE N'[]' END,'$.status')='IN_PROGRESS'
              AND JSON_VALUE(CASE WHEN ISJSON(new_values)=1 THEN new_values ELSE N'[]' END,'$.status')='COMPLETED'
            """).AnyAsync(ct);
    }
}
