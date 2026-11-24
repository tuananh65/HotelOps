using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed partial class RoomService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,RoomReadinessService readiness)
{
    public async Task<object> List(long branchId,CancellationToken ct){await access.Require("ROOM_VIEW",branchId,ct:ct);
        var canOperate=await access.HasRole("RECEPTION",branchId,ct)&&await CurrentReceptionShift(branchId,ct);
        return await (from r in db.Rooms.AsNoTracking() join v in db.VRoomReadinesses on r.RoomId equals v.RoomId where r.BranchId==branchId orderby r.Code
            select new{r.RoomId,r.BranchId,r.Code,r.RoomTypeId,r.LocationId,r.OperationalStatus,r.TurnoverNo,r.RowVersion,v.IsOccupied,v.Readiness,canOperate,isReserved=r.OperationalStatus=="RESERVED",
                floor=db.Locations.Where(l=>l.LocationId==r.LocationId).Select(l=>l.Name).FirstOrDefault(),
                roomTypeName=db.RoomTypes.Where(t=>t.RoomTypeId==r.RoomTypeId).Select(t=>t.Name).FirstOrDefault(),
                capacity=db.RoomTypes.Where(t=>t.RoomTypeId==r.RoomTypeId).Select(t=>t.Capacity).FirstOrDefault(),
                cleaningStatus=db.WorkItems.Where(w=>w.WorkId==r.CurrentCleaningWorkId&&w.TurnoverNo==r.TurnoverNo).Select(w=>w.Status).FirstOrDefault(),
                inspectionStatus=db.WorkItems.Where(w=>w.ParentWorkId==r.CurrentCleaningWorkId&&w.Kind=="INSPECTION"&&w.TurnoverNo==r.TurnoverNo).OrderByDescending(w=>w.WorkId).Select(w=>w.Status).FirstOrDefault(),
                isBlocked=v.Readiness=="BLOCKED"}).ToArrayAsync(ct);}
    public async Task<object> History(long id,CancellationToken ct){var r=await db.Rooms.FindAsync([id],ct)??throw new ApiException(404,"ROOM_NOT_FOUND");await access.Require("ROOM_VIEW",r.BranchId,ct:ct);
        return new{events=(await db.RoomEvents.AsNoTracking().Where(x=>x.RoomId==id).OrderByDescending(x=>x.RoomEventId).Take(200).ToArrayAsync(ct)).Select(Output.Record),blocks=(await db.RoomBlocks.AsNoTracking().Where(x=>x.RoomId==id).ToArrayAsync(ct)).Select(Output.Record)};}
    public Task<object> Act(long id,string action,RoomActionRequest r,CancellationToken ct)=>atomic.Run(async()=>{
        var room=await db.Rooms.SingleOrDefaultAsync(x=>x.RoomId==id,ct)??throw new ApiException(404,"ROOM_NOT_FOUND");
        await access.Require("ROOM_VIEW",room.BranchId,ct:ct);
        var permission=action switch{"reserve" or "cancel-reservation" or "check-in"=>"ROOM_CHECKIN","check-out"=>"ROOM_CHECKOUT","move"=>"ROOM_OCCUPANCY_MOVE","block" or "unblock"=>"ROOM_BLOCK_MANAGE",_=>throw new ApiException(400,"UNKNOWN_ACTION")};
        await access.Require(permission,room.BranchId,ct:ct);Output.Key(r.OperationKey);Output.Version(room.RowVersion,r.RowVersion);Output.Require(room.IsActive,"ROOM_INACTIVE");
        Output.Require(!await db.RoomEvents.AnyAsync(x=>x.RoomId==id&&x.ActorUserId==actor.Id&&x.OperationKey==r.OperationKey,ct),"OPERATION_ALREADY_APPLIED");
        var before=room.OperationalStatus;var now=clock.GetUtcNow().UtcDateTime;
        if(action is "reserve" or "cancel-reservation" or "check-in" or "check-out" or "move"){
            Output.Require(await access.HasRole("RECEPTION",room.BranchId,ct),"RECEPTION_REQUIRED",403);
            await access.RequireRolePermission("RECEPTION",permission,room.BranchId,ct);
            Output.Require(await CurrentReceptionShift(room.BranchId,ct),"RECEPTION_NOT_ON_SHIFT",403);
            if(action=="reserve"){
                await Available(room,ct);room.OperationalStatus="RESERVED";
            }else if(action=="cancel-reservation"){
                Output.Require(room.OperationalStatus=="RESERVED","ROOM_NOT_RESERVED");
                await ReservationReady(room,ct);room.OperationalStatus="READY";
            }else if(action=="check-in"){
                Output.Require(room.OperationalStatus=="RESERVED","ROOM_NOT_RESERVED");
                await ReservationReady(room,ct);room.OperationalStatus="OCCUPIED";
            }
            else {
                var reserved=room.OperationalStatus=="RESERVED";
                Output.Require(room.OperationalStatus=="OCCUPIED"||(action=="move"&&reserved),"ROOM_NOT_OCCUPIED");
                Output.Require(!await db.WorkItems.AnyAsync(w=>w.RoomId==id&&(w.Kind=="CLEANING"||w.Kind=="INSPECTION"||w.Kind=="REWORK")&&!WorkRules.Terminal.Contains(w.Status),ct),"ACTIVE_ROOM_WORK_BLOCKS_DEPARTURE");
                if(action=="move"){
                    Output.Reason(r.Reason);var target=await db.Rooms.SingleOrDefaultAsync(x=>x.RoomId==r.TargetRoomId&&x.BranchId==room.BranchId,ct)??throw new ApiException(404,"TARGET_ROOM_NOT_FOUND");
                    Output.Require(target.RoomId!=room.RoomId,"TARGET_ROOM_MUST_DIFFER",400);
                    Output.Version(target.RowVersion,r.TargetRowVersion??"");var previous=target.OperationalStatus;await Available(target,ct);
                    if(reserved)await ReservationReady(room,ct);
                    target.OperationalStatus=reserved?"RESERVED":"OCCUPIED";
                    History(target,reserved?"RESERVATION_MOVE_IN":"OCCUPANCY_MOVE_IN",previous,r);
                }
                if(reserved)room.OperationalStatus="READY";
                else {room.OperationalStatus="DIRTY";room.TurnoverNo++;room.CurrentCleaningWorkId=null;}
            }
        }else{
            Output.Reason(r.Reason);
            if(action=="block"){
                if(r.SourceWorkId!=null)Output.Require(await db.WorkItems.AnyAsync(w=>w.WorkId==r.SourceWorkId&&w.BranchId==room.BranchId&&w.RoomId==id,ct),"BLOCK_SOURCE_MISMATCH");
                db.RoomBlocks.Add(new RoomBlock{BranchId=room.BranchId,RoomId=id,SourceWorkId=r.SourceWorkId,Reason=r.Reason!,BlockedBy=actor.Id,BlockedAt=now});
            }else{
                Output.Require(!await db.WorkItems.AnyAsync(w=>w.RoomId==id&&!WorkRules.Terminal.Contains(w.Status)
                    &&db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="ROOM_ISSUE_REPORTED"),ct),"BLOCKING_WORK_NOT_VERIFIED");
                var blocks=await db.RoomBlocks.Where(x=>x.RoomId==id&&x.ReleasedAt==null).ToArrayAsync(ct);
                Output.Require(blocks.Length>0,"NO_ACTIVE_BLOCK");
                foreach(var block in blocks){
                    if(block.SourceWorkId!=null)Output.Require(await db.WorkItems.AnyAsync(w=>w.WorkId==block.SourceWorkId&&(w.Status=="CLOSED"||w.Status=="CANCELLED"),ct),"BLOCKING_WORK_NOT_VERIFIED");
                    block.ReleasedBy=actor.Id;block.ReleasedAt=now;
                }
            }
            db.Entry(room).Property(x=>x.Code).IsModified=true;
        }
        var eventType=action=="move"&&before=="RESERVED"?"RESERVATION_MOVE_OUT":action.ToUpperInvariant();
        History(room,eventType,before,r);audit.Add("Room",id,eventType,room.BranchId,after:r,key:r.OperationKey);
        if(action=="unblock")await readiness.Reevaluate(id,r.SourceWorkId,r.OperationKey,"READINESS_REEVALUATED",ct);
        await db.SaveChangesAsync(ct);return (object)new{room=Output.Record(room),readiness=Output.Record(await db.VRoomReadinesses.SingleAsync(x=>x.RoomId==id,ct))};
    },ct);
    private async Task Available(Room room,CancellationToken ct){
        var ready=await db.VRoomReadinesses.SingleAsync(x=>x.RoomId==room.RoomId,ct);
        Output.Require(room.IsActive&&room.OperationalStatus is "READY" or "AVAILABLE"&&ready.Readiness=="READY","ROOM_NOT_READY");
    }
    private async Task ReservationReady(Room room,CancellationToken ct)=>Output.Require(
        await db.VRoomReadinesses.AnyAsync(v=>v.RoomId==room.RoomId&&v.Readiness=="READY",ct),"ROOM_NOT_READY");
    private void History(Room room,string type,string previous,RoomActionRequest r)=>db.RoomEvents.Add(new RoomEvent{RoomId=room.RoomId,BranchId=room.BranchId,TurnoverNo=room.TurnoverNo,EventType=type,FromStatus=previous,ToStatus=room.OperationalStatus,ActorUserId=actor.Id,OperationKey=r.OperationKey,Reason=r.Reason,SourceWorkId=r.SourceWorkId,OccurredAt=clock.GetUtcNow().UtcDateTime});
}
