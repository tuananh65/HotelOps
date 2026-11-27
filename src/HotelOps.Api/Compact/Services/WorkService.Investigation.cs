using System.Text.Json;


using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    private async Task<object> Investigation(long id,CancellationToken ct)
    {
        var rows=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==id&&(e.EventType=="INCIDENT_CAUSE_VERIFIED"||e.EventType=="INCIDENT_TECHNICAL_OPINION"))
            .OrderByDescending(e=>e.WorkEventId).Take(100).Select(e=>new{e.WorkEventId,e.EventType,e.ActorUserId,e.OccurredAt,e.Reason,e.SnapshotJson}).ToArrayAsync(ct);
        return rows.Select(e=>new{e.WorkEventId,e.EventType,e.ActorUserId,e.OccurredAt,e.Reason,details=JsonSerializer.Deserialize<JsonElement>(e.SnapshotJson??"{}")}).ToArray();
    }
}
