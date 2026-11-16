using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal enum CleaningDeliveryQueue { Assignment, Closure, Cancellation }

public sealed partial class ProvisionalCleaningDelivery
{
    internal async Task<long[]> PendingAsync(CleaningDeliveryQueue queue,long after,CancellationToken ct)
    {
        var enabled=queue switch
        {
            CleaningDeliveryQueue.Assignment=>await candidate.AvailableAsync(ct),
            CleaningDeliveryQueue.Closure=>await candidate.ClosureAvailableAsync(ct),
            CleaningDeliveryQueue.Cancellation=>await candidate.CancellationAvailableAsync(ct),
            _=>false
        };
        if(!enabled)return [];
        var first=queue switch
        {
            CleaningDeliveryQueue.Assignment=>"CLEANING_TASK_ASSIGNED",
            CleaningDeliveryQueue.Closure=>"CLEANING_TASK_COMPLETED",
            _=>"CLEANING_TASK_CANCELLED"
        };
        var second=queue==CleaningDeliveryQueue.Closure?"CLEANING_BLOCKER_REPORTED":first;
        return await db.Database.SqlQuery<long>($"""
            SELECT TOP(100) i.intent_id AS Value FROM dbo.notification_delivery_intents i
            JOIN dbo.notification_publications p ON p.publication_id=i.publication_id
            WHERE i.intent_id>{after} AND i.status='PENDING' AND i.delivery_channel='IN_APP'
              AND i.next_attempt_at<=SYSUTCDATETIME()
              AND (i.claimed_until IS NULL OR i.claimed_until<SYSUTCDATETIME())
              AND p.event_type IN({first},{second}) ORDER BY i.intent_id
            """).ToArrayAsync(ct);
    }
}
