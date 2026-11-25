using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Contracts;
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
        await ReleaseReclassifiedReservations(work,key,ct);
        // The owning WorkService transition authorizes assign/handover before this internal hook.
        Output.Require(!await db.SupplyReservations.AnyAsync(x=>x.WorkId==workId&&x.Status=="CONSUMED",ct),"SUPPLY_ALREADY_CONSUMED");
        var a=await db.StaffSupplyAllocations.SingleOrDefaultAsync(x=>x.UserId==userId&&x.BranchId==work.BranchId&&x.Status!="CLOSED"&&db.WorkdayAssignments.Any(d=>d.AssignmentId==x.AssignmentId&&d.Status=="WORKING"),ct)??throw new ApiException(409,"STAFF_SUPPLY_ALLOCATION_REQUIRED");
        await AllocationActive(a,ct,allowFinishing);
        var balances=await db.StaffSupplyBalances.Where(x=>x.AllocationId==a.AllocationId&&db.ItemLifecycleProfiles.Any(p=>p.BranchId==a.BranchId&&p.ItemId==x.ItemId&&p.Classification=="CONSUMABLE")).OrderBy(x=>x.BalanceId).ToArrayAsync(ct);
        Output.Require(balances.Length>0,"SUPPLY_CONFIGURATION_REQUIRED");
        var room=await db.Rooms.AsNoTracking().SingleOrDefaultAsync(x=>x.RoomId==work.RoomId&&x.BranchId==work.BranchId&&x.IsActive,ct)??throw new ApiException(409,"ROOM_UNAVAILABLE");
        var capacity=await db.RoomTypes.Where(x=>x.RoomTypeId==room.RoomTypeId&&x.BranchId==room.BranchId&&x.IsActive).Select(x=>(int?)x.Capacity).SingleOrDefaultAsync(ct);
        Output.Require(capacity is >0,"ROOM_SUPPLY_CAPACITY_INVALID");
        var old=await db.SupplyReservations.Where(x=>x.WorkId==workId&&x.Status=="RESERVED").ToArrayAsync(ct);
        if(old.Length==balances.Length&&old.All(r=>balances.Any(b=>b.BalanceId==r.BalanceId)))return false;
        var snapshot=await AssessmentSnapshot(workId,ct);
        var oldItems=new Dictionary<long,SupplyReservation>();
        foreach(var r in old)oldItems.Add(await db.StaffSupplyBalances.Where(b=>b.BalanceId==r.BalanceId).Select(b=>b.ItemId).SingleAsync(ct),r);
        Output.Require(oldItems.Keys.All(item=>balances.Any(b=>b.ItemId==item)),"STAFF_SUPPLY_SNAPSHOT_ITEMS_MISSING");
        var assessed=new Dictionary<long,(SupplyReservation Reservation,AssessRoomSupplyLine Input)>();
        foreach(var r in old){
            if(r.AssessedAt==null||!snapshot.TryGetValue(r.ReservationId,out var input))continue;
            RoomSupplyAssessmentRules.Validate(SupplyMath.Required(r.Capacity,r.PerGuestQuantity),input);
            var item=await db.StaffSupplyBalances.Where(b=>b.BalanceId==r.BalanceId).Select(b=>b.ItemId).SingleAsync(ct);
            assessed[item]=(r,input);
        }
        decimal Required(StaffSupplyBalance b)=>assessed.TryGetValue(b.ItemId,out var previous)
            ?Math.Max(0,SupplyMath.Required(previous.Reservation.Capacity,previous.Reservation.PerGuestQuantity)-previous.Input.UsableQuantity-previous.Input.ReplenishedQuantity!.Value)
            :oldItems.TryGetValue(b.ItemId,out var prior)?prior.Quantity:SupplyMath.Required(capacity!.Value,b.PerGuestQuantity);
        // Issue just the deficit against usable, unreserved custody in this transaction.
        var issue=new List<(StaffSupplyBalance Balance,decimal Quantity)>();
        foreach(var b in balances){
            var missing=Math.Max(0,Required(b)-await StockBatchAccounting.StaffAvailable(db,b,Now,ct));
            if(missing==0)continue;
            Output.Require(await WarehouseAvailable(b,ct),"WAREHOUSE_UNAVAILABLE");
            Output.Require(await Stock(b,ct)>=missing,"STOCK_INSUFFICIENT");
            issue.Add((b,missing));
        }
        if(issue.Count>0){
            Output.Require(!await db.SupplyStockDocuments.AnyAsync(d=>d.AllocationId==a.AllocationId&&d.RequestWorkId==workId&&d.Purpose=="ADDITIONAL",ct),"STAFF_SUPPLIES_INSUFFICIENT");
            var lines=await documents.PostStaffSupply(a,issue,"ADDITIONAL",key,workId,ct);
            foreach(var (balance,quantity) in issue){
                balance.AdditionalIssued+=quantity;
                Ledger(balance,"ADDITIONAL",quantity,key,workId:workId,documentLineId:lines[balance.BalanceId]);
            }
            await db.SaveChangesAsync(ct);
        }
        await AllocateReusableForTask(a,workId,capacity!.Value,key,ct);
        a.Status="OPEN";a.ShortageReason=null;
        // Validate the new employee before touching the old employee's reservations.
        foreach(var b in balances)Output.Require(await StockBatchAccounting.StaffAvailable(db,b,Now,ct)>=Required(b),"STAFF_SUPPLIES_INSUFFICIENT");
        // Settle only quantities the previous employee recorded as physically placed. The
        // remaining deficit, not the full room norm, transfers to the new employee.
        foreach(var (r,input) in assessed.Values){
            var used=input.ReplenishedQuantity!.Value;
            if(used==0)continue;
            var previous=await db.StaffSupplyBalances.SingleAsync(b=>b.BalanceId==r.BalanceId,ct);
            Output.Require(previous.Reserved>=r.Quantity&&SupplyMath.Remaining(previous)>=used,"SUPPLY_BALANCE_INCONSISTENT");
            previous.Used+=used;previous.Reserved-=used;r.Quantity-=used;r.UsableQuantity+=used;
            Ledger(previous,"USE",used,key,workId,r.ReservationId);await db.SaveChangesAsync(ct);
            var entry=await db.StaffSupplyLedgers.SingleAsync(l=>l.BalanceId==previous.BalanceId&&l.ReservationId==r.ReservationId&&l.Kind=="USE"&&l.OperationKey==key,ct);
            await new StockBatchAccounting(db,clock).Consume(entry,ct);
            audit.Add("SupplyReservation",r.ReservationId,"REASSIGNMENT_PLACEMENT_SETTLED",work.BranchId,after:new{used,previous.ItemId,entry.LedgerId},key:key);
        }
        foreach(var r in old)await ReleaseReservation(r,key,ct);
        await db.SaveChangesAsync(ct);
        var carried=new List<AssessRoomSupplyLine>();
        foreach(var b in balances){
            var quantity=Required(b);
            var reservation=new SupplyReservation{BalanceId=b.BalanceId,WorkId=workId,RoomId=room.RoomId,Capacity=capacity!.Value,PerGuestQuantity=b.PerGuestQuantity,Quantity=quantity,CreatedAt=Now};
            if(oldItems.TryGetValue(b.ItemId,out var prior)){reservation.Capacity=prior.Capacity;reservation.PerGuestQuantity=prior.PerGuestQuantity;}
            if(assessed.TryGetValue(b.ItemId,out var previous)){
                reservation.Capacity=previous.Reservation.Capacity;reservation.PerGuestQuantity=previous.Reservation.PerGuestQuantity;
                reservation.UsableQuantity=previous.Input.UsableQuantity+previous.Input.ReplenishedQuantity!.Value;
                reservation.AssessedAt=Now;reservation.AssessedBy=previous.Reservation.AssessedBy;
            }
            db.SupplyReservations.Add(reservation);b.Reserved+=quantity;
            await db.SaveChangesAsync(ct);if(quantity>0)Ledger(b,"RESERVE",quantity,key,workId,reservation.ReservationId);
            if(assessed.TryGetValue(b.ItemId,out previous))carried.Add(previous.Input with{ReservationId=reservation.ReservationId,
                UsableQuantity=reservation.UsableQuantity!.Value,CurrentQuantity=previous.Input.CurrentQuantity+previous.Input.ReplenishedQuantity,
                ReplenishedQuantity=0,RowVersion=Convert.ToBase64String(reservation.RowVersion)});
        }
        if(carried.Count>0)audit.Add("WorkItem",workId,"SUPPLIES_ASSESSED",work.BranchId,after:carried.OrderBy(x=>x.ReservationId),key:key);
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
        await ReleaseReclassifiedReservations(work,key,ct);
        if(await db.SupplyReservations.AnyAsync(x=>x.WorkId==workId&&x.Status=="CONSUMED",ct))return false;
        var rows=await db.SupplyReservations.Where(x=>x.WorkId==workId&&x.Status=="RESERVED").ToArrayAsync(ct);
        Output.Require(rows.Length>0,"CLEANING_SUPPLY_RESERVATION_REQUIRED");
        Output.Require(rows.All(x=>x.AssessedAt!=null),"ROOM_SUPPLY_ASSESSMENT_REQUIRED");
        var assessment=await AssessmentSnapshot(workId,ct);
        foreach(var reservation in rows){
            var recorded=assessment.GetValueOrDefault(reservation.ReservationId)??throw new ApiException(409,"ROOM_SUPPLY_ASSESSMENT_REQUIRED");
            var missing=RoomSupplyAssessmentRules.Validate(SupplyMath.Required(reservation.Capacity,reservation.PerGuestQuantity),recorded);
            Output.Require(missing==reservation.Quantity,"SUPPLY_BALANCE_INCONSISTENT");
            if(recorded.ReplenishedQuantity<missing){
                var reports=await (from link in db.WorkLinks join issue in db.WorkItems on link.SourceWorkId equals issue.WorkId
                    join report in db.WorkEvents on issue.WorkId equals report.WorkId
                    where link.TargetWorkId==workId&&link.Relation=="SOURCE"&&issue.BranchId==work.BranchId&&issue.RoomId==work.RoomId&&issue.Status!="CANCELLED"&&issue.Status!="CLOSED"&&report.EventType=="ROOM_ISSUE_REPORTED"
                    select report.SnapshotJson).ToArrayAsync(ct);
                var itemId=await db.StaffSupplyBalances.Where(b=>b.BalanceId==reservation.BalanceId).Select(b=>b.ItemId).SingleAsync(ct);
                var reportedMissing=reports.Where(j=>j!=null).Select(j=>System.Text.Json.JsonSerializer.Deserialize<MissingSupplyReport>(j!)!)
                    .Where(r=>r.Kind=="MISSING"&&r.ItemId==itemId).Sum(r=>r.Quantity);
                Output.Require(reportedMissing>=missing-recorded.ReplenishedQuantity,"ROOM_SUPPLIES_NOT_REPLENISHED");
            }
            var used=recorded.ReplenishedQuantity!.Value;
            var b=await db.StaffSupplyBalances.SingleAsync(x=>x.BalanceId==reservation.BalanceId,ct);
            var a=await LoadAllocation(b.AllocationId,ct);
            Output.Require(a.UserId==actor.Id&&a.BranchId==work.BranchId,"SUPPLY_OWNER_REQUIRED",403);
            await AllocationActive(a,ct,allowFinishing:true);
            Output.Require(b.Reserved>=reservation.Quantity&&SupplyMath.Remaining(b)>=reservation.Quantity,"SUPPLY_BALANCE_INCONSISTENT");
            b.Reserved-=reservation.Quantity;b.Used+=used;
            if(used<reservation.Quantity)Ledger(b,"RELEASE",reservation.Quantity-used,key,workId,reservation.ReservationId);
            reservation.Status="CONSUMED";reservation.ResolvedAt=Now;
            if(used>0){
                Ledger(b,"USE",used,key,workId,reservation.ReservationId);await db.SaveChangesAsync(ct);
                var ledger=await db.StaffSupplyLedgers.SingleAsync(l=>l.BalanceId==b.BalanceId&&l.ReservationId==reservation.ReservationId&&l.Kind=="USE"&&l.OperationKey==key,ct);
                await new StockBatchAccounting(db,clock).Consume(ledger,ct);
            }
        }
        audit.Add("WorkItem",workId,"SUPPLIES_PLACED_IN_ROOM",work.BranchId,key:key);await db.SaveChangesAsync(ct);return true;
    },ct);

    // A classification correction releases a legacy consumption hold only during an
    // authorized work mutation. Historical consumption and physical custody stay intact.
    private async Task ReleaseReclassifiedReservations(WorkItem work,Guid key,CancellationToken ct)
    {
        var obsolete=await (from r in db.SupplyReservations join b in db.StaffSupplyBalances on r.BalanceId equals b.BalanceId
            join p in db.ItemLifecycleProfiles on new{work.BranchId,b.ItemId} equals new{p.BranchId,p.ItemId}
            where r.WorkId==work.WorkId&&r.Status=="RESERVED"&&p.Classification!="CONSUMABLE" select r).ToArrayAsync(ct);
        foreach(var r in obsolete){
            await ReleaseReservation(r,key,ct);
            audit.Add("SupplyReservation",r.ReservationId,"CLASSIFICATION_HOLD_RELEASED",work.BranchId,after:new{r.Quantity,r.Status},key:key);
        }
        if(obsolete.Length>0)await db.SaveChangesAsync(ct);
    }

    private async Task ReleaseReservation(SupplyReservation reservation,Guid key,CancellationToken ct)
    {
        var b=await db.StaffSupplyBalances.SingleAsync(x=>x.BalanceId==reservation.BalanceId,ct);
        Output.Require(b.Reserved>=reservation.Quantity,"SUPPLY_BALANCE_INCONSISTENT");
        b.Reserved-=reservation.Quantity;reservation.Status="RELEASED";reservation.ResolvedAt=Now;
        if(reservation.Quantity>0)Ledger(b,"RELEASE",reservation.Quantity,key,reservation.WorkId,reservation.ReservationId);
    }
    private sealed record MissingSupplyReport(string Kind,long? ItemId,decimal Quantity);
}
