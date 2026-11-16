using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed partial class IncidentCancellationService
{
    private async Task ReleaseBlocksAsync(Incident incident,long actor,string reason,DateTime now,CancellationToken ct)
    {
        var rooms=await db.RoomBlocks.AsNoTracking().Where(b=>b.IncidentId==incident.IncidentId && b.BlockedAt<=now && (b.ReleasedAt==null || b.ReleasedAt>now))
            .Select(b=>b.RoomId).Distinct().OrderBy(id=>id).ToArrayAsync(ct);
        var readiness=new EffectiveRoomReadiness(db);
        foreach(var id in rooms)
        {
            var room=await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={id}").AsTracking().SingleAsync(ct);
            if(room.BranchId!=incident.BranchId) throw new InternalRequestReadException(409,"BLOCK_SCOPE_INCONSISTENT");
            var blocks=await db.RoomBlocks.FromSqlInterpolated($"SELECT * FROM dbo.room_blocks WITH(UPDLOCK,HOLDLOCK) WHERE room_id={id}").AsTracking().ToArrayAsync(ct);
            var before=(await readiness.ReadAsync([id],now,ct))[id];
            var released=blocks.Where(b=>b.IncidentId==incident.IncidentId && b.BlockedAt<=now && (b.ReleasedAt==null || b.ReleasedAt>now)).ToArray();
            foreach(var block in released)
            {
                var old=JsonSerializer.Serialize(new {block.RoomId,block.IncidentId,block.ReleasedAt,block.ReleasedBy,block.ReleaseNote});
                block.ReleasedAt=now;block.ReleasedBy=actor;block.ReleaseNote=reason;
                db.AuditLogs.Add(new AuditLog {EntityType="room_blocks",EntityId=block.BlockId.ToString(CultureInfo.InvariantCulture),Action="UPDATE",
                    BranchId=incident.BranchId,ActorUserId=actor,OccurredAt=now,OldValues=old,
                    NewValues=JsonSerializer.Serialize(new {block.RoomId,block.IncidentId,block.ReleasedAt,block.ReleasedBy,block.ReleaseNote})});
            }
            await db.SaveChangesAsync(ct);
            var after=(await readiness.ReadAsync([id],now,ct))[id];
            var physicalBefore=room.OperationalStatus;
            // Only proven clean/Passed readiness without any remaining block permits READY.
            if(released.Length>0 && after.CanUse && physicalBefore!="READY")
            {
                room.OperationalStatus="READY";
                db.RoomStatusHistories.Add(new RoomStatusHistory {RoomId=id,FromStatus=physicalBefore,ToStatus="READY",
                    TriggerType="ROOM_BLOCK",TriggerId=released[0].BlockId,ChangedBy=actor,ChangedAt=now,Reason=reason});
            }
            db.AuditLogs.Add(new AuditLog {EntityType="rooms",EntityId=id.ToString(CultureInfo.InvariantCulture),Action="UPDATE",BranchId=incident.BranchId,
                ActorUserId=actor,OccurredAt=now,OldValues=JsonSerializer.Serialize(new {effective_readiness=before.Readiness,operational_status=physicalBefore}),
                NewValues=JsonSerializer.Serialize(new {effective_readiness=after.Readiness,operational_status=room.OperationalStatus,
                    incident_id=incident.IncidentId,released_block_ids=released.Select(b=>b.BlockId).ToArray(),cause="INCIDENT_CANCELLED"})});
        }
    }
}
