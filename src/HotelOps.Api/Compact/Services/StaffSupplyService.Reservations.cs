using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    // All mutation hooks run inside the task transition's Atomic transaction. Calling independently
    // is safe too, but callers must not commit a work transition separately from its supply hook.
    internal Task Reserve(long workId,long userId,Guid key,CancellationToken ct,bool allowFinishing=false)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var work=await db.WorkItems.SingleOrDefaultAsync(x=>x.WorkId==workId,ct)??throw new ApiException(404,"WORK_NOT_FOUND");
        if(work.Kind=="REWORK")return false; // Reinspection/rework never consumes another full kit.
        Output.Require(work.Kind=="CLEANING"&&work.RoomId!=null&&!WorkRules.Terminal.Contains(work.Status),"CLEANING_WORK_REQUIRED");
        // The owning WorkService transition authorizes assign/handover before this internal hook.
        Output.Require(!await db.SupplyReservations.AnyAsync(x=>x.WorkId==workId&&x.Status=="CONSUMED",ct),"SUPPLY_ALREADY_CONSUMED");
        var a=await db.StaffSupplyAllocations.SingleOrDefaultAsync(x=>x.UserId==userId&&x.BranchId==work.BranchId&&x.Status!="CLOSED"&&db.WorkdayAssignments.Any(d=>d.AssignmentId==x.AssignmentId&&d.Status=="WORKING"),ct)??throw new ApiException(409,"STAFF_SUPPLY_ALLOCATION_REQUIRED");
        await AllocationActive(a,ct,allowFinishing);
        var balances=await db.StaffSupplyBalances.Where(x=>x.AllocationId==a.AllocationId).OrderBy(x=>x.BalanceId).ToArrayAsync(ct);
        Output.Require(balances.Length>0,"SUPPLY_CONFIGURATION_REQUIRED");
        var room=await db.Rooms.AsNoTracking().SingleOrDefaultAsync(x=>x.RoomId==work.RoomId&&x.BranchId==work.BranchId&&x.IsActive,ct)??throw new ApiException(409,"ROOM_UNAVAILABLE");
        var capacity=await db.RoomTypes.Where(x=>x.RoomTypeId==room.RoomTypeId&&x.BranchId==room.BranchId&&x.IsActive).Select(x=>(int?)x.Capacity).SingleOrDefaultAsync(ct);
        Output.Require(capacity is >0,"ROOM_SUPPLY_CAPACITY_INVALID");
        var old=await db.SupplyReservations.Where(x=>x.WorkId==workId&&x.Status=="RESERVED").ToArrayAsync(ct);
        if(old.Length==balances.Length&&old.All(r=>balances.Any(b=>b.BalanceId==r.BalanceId)))return false;
        // Validate the new employee before touching the old employee's reservations.
        foreach(var b in balances)Output.Require(SupplyMath.Available(b)>=SupplyMath.Required(capacity!.Value,b.PerGuestQuantity),"STAFF_SUPPLIES_INSUFFICIENT");
        foreach(var r in old)await ReleaseReservation(r,key,ct);
        await db.SaveChangesAsync(ct);
        foreach(var b in balances){
            var quantity=SupplyMath.Required(capacity!.Value,b.PerGuestQuantity);
            var reservation=new SupplyReservation{BalanceId=b.BalanceId,WorkId=workId,RoomId=room.RoomId,Capacity=capacity.Value,PerGuestQuantity=b.PerGuestQuantity,Quantity=quantity,CreatedAt=Now};
            db.SupplyReservations.Add(reservation);b.Reserved+=quantity;
            await db.SaveChangesAsync(ct);Ledger(b,"RESERVE",quantity,key,workId,reservation.ReservationId);
        }
        audit.Add("WorkItem",workId,"SUPPLIES_RESERVED",work.BranchId,key:key);await db.SaveChangesAsync(ct);return true;
    },ct);

    internal Task Release(long workId,Guid key,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var reservations=await db.SupplyReservations.Where(x=>x.WorkId==workId&&x.Status=="RESERVED").ToArrayAsync(ct);
        foreach(var reservation in reservations)await ReleaseReservation(reservation,key,ct);
        await db.SaveChangesAsync(ct);return true;
    },ct);

    internal Task Consume(long workId,Guid key,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var work=await db.WorkItems.SingleOrDefaultAsync(x=>x.WorkId==workId,ct)??throw new ApiException(404,"WORK_NOT_FOUND");
        if(work.Kind=="REWORK")return false;
        Output.Require(work.Kind=="CLEANING","CLEANING_WORK_REQUIRED");
        if(await db.SupplyReservations.AnyAsync(x=>x.WorkId==workId&&x.Status=="CONSUMED",ct))return false;
        var rows=await db.SupplyReservations.Where(x=>x.WorkId==workId&&x.Status=="RESERVED").ToArrayAsync(ct);
        Output.Require(rows.Length>0,"CLEANING_SUPPLY_RESERVATION_REQUIRED");
        foreach(var reservation in rows){
            var b=await db.StaffSupplyBalances.SingleAsync(x=>x.BalanceId==reservation.BalanceId,ct);
            var a=await LoadAllocation(b.AllocationId,ct);
            Output.Require(a.UserId==actor.Id&&a.BranchId==work.BranchId,"SUPPLY_OWNER_REQUIRED",403);
            await AllocationActive(a,ct,allowFinishing:true);
            Output.Require(b.Reserved>=reservation.Quantity&&SupplyMath.Remaining(b)>=reservation.Quantity,"SUPPLY_BALANCE_INCONSISTENT");
            b.Reserved-=reservation.Quantity;b.Used+=reservation.Quantity;
            reservation.Status="CONSUMED";reservation.ResolvedAt=Now;
            Ledger(b,"USE",reservation.Quantity,key,workId,reservation.ReservationId);
        }
        audit.Add("WorkItem",workId,"SUPPLIES_PLACED_IN_ROOM",work.BranchId,key:key);await db.SaveChangesAsync(ct);return true;
    },ct);

    private async Task ReleaseReservation(SupplyReservation reservation,Guid key,CancellationToken ct)
    {
        var b=await db.StaffSupplyBalances.SingleAsync(x=>x.BalanceId==reservation.BalanceId,ct);
        Output.Require(b.Reserved>=reservation.Quantity,"SUPPLY_BALANCE_INCONSISTENT");
        b.Reserved-=reservation.Quantity;reservation.Status="RELEASED";reservation.ResolvedAt=Now;
        Ledger(b,"RELEASE",reservation.Quantity,key,reservation.WorkId,reservation.ReservationId);
    }
}
