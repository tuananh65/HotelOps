using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class RoomService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock)
{
    public async Task<object> List(long branchId,CancellationToken ct){await access.Require("ROOM_VIEW",branchId,ct:ct);
        return await (from r in db.Rooms.AsNoTracking() join v in db.VRoomReadinesses on r.RoomId equals v.RoomId where r.BranchId==branchId orderby r.Code
            select new{r.RoomId,r.BranchId,r.Code,r.RoomTypeId,r.LocationId,r.OperationalStatus,r.TurnoverNo,r.RowVersion,v.IsOccupied,v.Readiness,
                isBlocked=db.RoomBlocks.Any(b=>b.RoomId==r.RoomId&&b.ReleasedAt==null)}).ToArrayAsync(ct);}
    public async Task<object> History(long id,CancellationToken ct){var r=await db.Rooms.FindAsync([id],ct)??throw new ApiException(404,"ROOM_NOT_FOUND");await access.Require("ROOM_VIEW",r.BranchId,ct:ct);
        return new{events=(await db.RoomEvents.AsNoTracking().Where(x=>x.RoomId==id).OrderByDescending(x=>x.RoomEventId).Take(200).ToArrayAsync(ct)).Select(Output.Record),blocks=(await db.RoomBlocks.AsNoTracking().Where(x=>x.RoomId==id).ToArrayAsync(ct)).Select(Output.Record)};}
    public Task<object> Act(long id,string action,RoomActionRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var room=await db.Rooms.SingleOrDefaultAsync(x=>x.RoomId==id,ct)??throw new ApiException(404,"ROOM_NOT_FOUND");
        await access.Require("ROOM_VIEW",room.BranchId,ct:ct);
        var permission=action switch{"check-in"=>"ROOM_CHECKIN","check-out"=>"ROOM_CHECKOUT","move"=>"ROOM_OCCUPANCY_MOVE","block" or "unblock"=>"ROOM_BLOCK_MANAGE",_=>throw new ApiException(400,"UNKNOWN_ACTION")};
        await access.Require(permission,room.BranchId,ct:ct);Output.Key(r.OperationKey);Output.Version(room.RowVersion,r.RowVersion);Output.Require(room.IsActive,"ROOM_INACTIVE");
        Output.Require(!await db.RoomEvents.AnyAsync(x=>x.RoomId==id&&x.ActorUserId==actor.Id&&x.OperationKey==r.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        var before=room.OperationalStatus;var now=clock.GetUtcNow().UtcDateTime;
        if(action is "check-in" or "check-out" or "move"){
            Output.Require(await access.HasRole("RECEPTION",room.BranchId,ct),"RECEPTION_REQUIRED",403);
            await access.RequireRolePermission("RECEPTION",permission,room.BranchId,ct);
            if(action=="check-in")await Arrival(room,ct);
            else {
                Output.Require(room.OperationalStatus=="OCCUPIED","ROOM_NOT_OCCUPIED");
                Output.Require(!await db.WorkItems.AnyAsync(w=>w.RoomId==id&&(w.Kind=="CLEANING"||w.Kind=="INSPECTION"||w.Kind=="REWORK")&&!WorkRules.Terminal.Contains(w.Status),ct),"ACTIVE_ROOM_WORK_BLOCKS_DEPARTURE");
                if(action=="move"){
                    Output.Reason(r.Reason);var target=await db.Rooms.SingleOrDefaultAsync(x=>x.RoomId==r.TargetRoomId&&x.BranchId==room.BranchId,ct)??throw new ApiException(404,"TARGET_ROOM_NOT_FOUND");
                    Output.Version(target.RowVersion,r.TargetRowVersion??"");var previous=target.OperationalStatus;await Arrival(target,ct);History(target,"OCCUPANCY_MOVE_IN",previous,r);
                }
                room.OperationalStatus="DIRTY";room.TurnoverNo++;room.CurrentCleaningWorkId=null;
            }
        }else{
            Output.Reason(r.Reason);
            if(action=="block"){
                if(r.SourceWorkId!=null)Output.Require(await db.WorkItems.AnyAsync(w=>w.WorkId==r.SourceWorkId&&w.BranchId==room.BranchId&&w.RoomId==id,ct),"BLOCK_SOURCE_MISMATCH");
                db.RoomBlocks.Add(new RoomBlock{BranchId=room.BranchId,RoomId=id,SourceWorkId=r.SourceWorkId,Reason=r.Reason!,BlockedBy=actor.Id,BlockedAt=now});
            }else{
                var block=await db.RoomBlocks.SingleOrDefaultAsync(x=>x.RoomId==id&&x.ReleasedAt==null,ct)??throw new ApiException(409,"NO_ACTIVE_BLOCK");
                if(block.SourceWorkId!=null)Output.Require(await db.WorkItems.AnyAsync(w=>w.WorkId==block.SourceWorkId&&w.Status=="CLOSED",ct),"BLOCKING_WORK_NOT_VERIFIED");
                block.ReleasedBy=actor.Id;block.ReleasedAt=now;
            }
            db.Entry(room).Property(x=>x.Code).IsModified=true;
        }
        History(room,action.ToUpperInvariant(),before,r);audit.Add("Room",id,action.ToUpperInvariant(),room.BranchId,after:r,key:r.OperationKey);
        await db.SaveChangesAsync(ct);return (object)new{room=Output.Record(room),readiness=Output.Record(await db.VRoomReadinesses.SingleAsync(x=>x.RoomId==id,ct))};
    },ct);
    private async Task Arrival(Room room,CancellationToken ct){
        var ready=await db.VRoomReadinesses.SingleAsync(x=>x.RoomId==room.RoomId,ct);
        Output.Require(room.IsActive&&room.OperationalStatus is "READY" or "AVAILABLE"&&ready.Readiness=="READY","ROOM_NOT_READY");room.OperationalStatus="OCCUPIED";
    }
    private void History(Room room,string type,string previous,RoomActionRequest r)=>db.RoomEvents.Add(new RoomEvent{RoomId=room.RoomId,BranchId=room.BranchId,TurnoverNo=room.TurnoverNo,EventType=type,FromStatus=previous,ToStatus=room.OperationalStatus,ActorUserId=actor.Id,OperationKey=r.OperationKey,Reason=r.Reason,SourceWorkId=r.SourceWorkId,OccurredAt=clock.GetUtcNow().UtcDateTime});
}
