using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

// The database view is the shared readiness policy used by both reads and the
// admission trigger. Call only inside the workflow transaction, after recording
// its result; never release a block or change occupancy on behalf of a caller.
public sealed class RoomReadinessService(HotelOpsCompactDbContext db, Actor actor, TimeProvider clock)
{
    public async Task Reevaluate(long roomId, long? sourceWorkId, Guid operationKey, string eventType, CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction != null, "READINESS_TRANSACTION_REQUIRED");
        await db.SaveChangesAsync(ct);
        var room = await db.Rooms.SingleAsync(r => r.RoomId == roomId, ct);
        if (!room.IsActive || room.OperationalStatus is "OCCUPIED" or "RESERVED" or "OUT_OF_SERVICE" or "READY") return;
        var facts = await db.VRoomReadinesses.AsNoTracking().SingleAsync(r => r.RoomId == roomId, ct);
        if (facts.Readiness != "READY") return;
        db.RoomEvents.Add(new RoomEvent {
            BranchId = room.BranchId, RoomId = room.RoomId, SourceWorkId = sourceWorkId,
            TurnoverNo = room.TurnoverNo, EventType = eventType,
            FromStatus = room.OperationalStatus, ToStatus = "READY", ActorUserId = actor.Id,
            OperationKey = operationKey, OccurredAt = clock.GetUtcNow().UtcDateTime
        });
        room.OperationalStatus = "READY";
        await db.SaveChangesAsync(ct);
    }
}
