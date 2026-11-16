using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Incidents;

public sealed partial class IncidentRoomBlockService
{
    private async Task ChangeRoomAsync(Incident incident,Room room,long actor,string action,string reason,DateTime now,CancellationToken ct)
    {
        var blocks=await db.RoomBlocks.FromSqlInterpolated($"SELECT * FROM dbo.room_blocks WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room.RoomId}").AsTracking().ToArrayAsync(ct);
        var effective=blocks.Where(b=>b.IncidentId==incident.IncidentId && b.BlockedAt<=now && (b.ReleasedAt==null || b.ReleasedAt>now)).ToArray();
        if(action=="BLOCK" && effective.Length>0) throw new ValidationException("Incident already blocks this room.");
        if(action=="REMOVE" && effective.Length!=1) throw new ValidationException("Exactly one effective Incident block is required for removal.");
        var readiness=new EffectiveRoomReadiness(db);
        var before=(await readiness.ReadAsync([room.RoomId],now,ct))[room.RoomId];
        RoomBlock block;
        if(action=="BLOCK")
        {
            block=new RoomBlock{RoomId=room.RoomId,IncidentId=incident.IncidentId,Reason=reason,BlockedAt=now,BlockedBy=actor};
            db.RoomBlocks.Add(block);await db.SaveChangesAsync(ct);
            Audit("room_blocks",block.BlockId,incident.BranchId,actor,now,new{effective=false},new{effective=true,incident_id=incident.IncidentId,room_id=room.RoomId,reason});
        }
        else
        {
            block=effective[0];
            var old=new{block.ReleasedAt,block.ReleasedBy,block.ReleaseNote};
            block.ReleasedAt=now;block.ReleasedBy=actor;block.ReleaseNote=reason;
            Audit("room_blocks",block.BlockId,incident.BranchId,actor,now,old,new{block.ReleasedAt,block.ReleasedBy,block.ReleaseNote});
        }
        await db.SaveChangesAsync(ct);
        var after=(await readiness.ReadAsync([room.RoomId],now,ct))[room.RoomId];
        var physical=room.OperationalStatus;
        if(after.CanUse && physical!="READY")
        {
            room.OperationalStatus="READY";
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.room_status_history(room_id,from_status,to_status,trigger_type,trigger_id,changed_by,changed_at,reason,effective_from,effective_to)
            VALUES({room.RoomId},{physical},{room.OperationalStatus},'ROOM_BLOCK',{block.BlockId},{actor},{now},{reason},{before.Readiness},{after.Readiness})
            """,ct);
        // Physical states do not encode BLOCKED. Effective old/new readiness remains in source audit.
        Audit("rooms",room.RoomId,incident.BranchId,actor,now,new{effective_readiness=before.Readiness,operational_status=physical},
            new{effective_readiness=after.Readiness,operational_status=room.OperationalStatus,incident_id=incident.IncidentId,block_id=block.BlockId,
                action,reason,notification_kind="INCIDENT_ROOM_BLOCK_CHANGED",notification_version=1});
    }
}
