using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService
{
    private sealed record ConfirmationSnapshot(long WorkId,long RoomId,long EmployeeId,long AssignmentId,long ShiftAssignmentId,
        long ShiftId,long? ExecutionId,string Payload,RoomSupplyAssessmentDto Result);
    private sealed record AssessmentSource(StaffSupplyBalance? Balance,SupplyReservation? Reservation,long ItemId,long UnitId,
        decimal PerGuest,int Capacity);
    private async Task<WorkItem> AssessmentWork(long id,CancellationToken ct)
    {
        var work=await db.WorkItems.SingleOrDefaultAsync(w=>w.WorkId==id,ct)??throw new ApiException(404,"WORK_NOT_FOUND");
        var owns=await db.WorkAssignments.AnyAsync(a=>a.WorkId==id&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct);
        await access.Require("CLEANING_VIEW",work.BranchId,owns,ct);
        Output.Require(work.Kind=="CLEANING"&&work.RoomId!=null,"CLEANING_WORK_REQUIRED",400);
        return work;
    }
    private Task<AuditLog?> Confirmation(long id,CancellationToken ct)=>db.AuditLogs.AsNoTracking()
        .Where(a=>a.EntityType=="WorkItem"&&a.EntityKey==id.ToString()&&a.Action=="SUPPLIES_CONFIRMED")
        .OrderByDescending(a=>a.AuditId).FirstOrDefaultAsync(ct);
    private static ConfirmationSnapshot ReadConfirmation(AuditLog audit)=>JsonSerializer.Deserialize<ConfirmationSnapshot>(audit.AfterJson!)
        ??throw new ApiException(409,"SUPPLY_CONFIRMATION_SNAPSHOT_INVALID");

    private async Task<(StaffSupplyAllocation? Allocation,AssessmentSource[] Sources)> AssessmentSources(WorkItem work,CancellationToken ct)
    {
        var owner=await db.WorkAssignments.Where(a=>a.WorkId==work.WorkId&&a.AssignmentStatus=="ACTIVE").Select(a=>(long?)a.AssigneeUserId).SingleOrDefaultAsync(ct);
        var current=await db.StaffSupplyAllocations.Where(a=>a.UserId==owner&&a.BranchId==work.BranchId&&a.Status!="CLOSED"
            &&db.WorkdayAssignments.Any(d=>d.AssignmentId==a.AssignmentId&&d.Status=="WORKING")).SingleOrDefaultAsync(ct);
        var capacity=await (from r in db.Rooms join t in db.RoomTypes on r.RoomTypeId equals t.RoomTypeId
            where r.RoomId==work.RoomId&&r.BranchId==work.BranchId&&t.BranchId==work.BranchId&&r.IsActive&&t.IsActive select t.Capacity).SingleOrDefaultAsync(ct);
        Output.Require(capacity>0,"ROOM_SUPPLY_CAPACITY_INVALID");
        if(current!=null){
            var balances=await db.StaffSupplyBalances.Where(b=>b.AllocationId==current.AllocationId
                &&db.ItemLifecycleProfiles.Any(p=>p.ItemId==b.ItemId&&p.BranchId==work.BranchId&&p.Classification=="CONSUMABLE")).OrderBy(b=>b.ItemId).ToArrayAsync(ct);
            if(balances.Length>0)return(current,balances.Select(b=>new AssessmentSource(b,null,b.ItemId,b.UnitId,b.PerGuestQuantity,capacity)).ToArray());
        }
        var config=await db.SupplyConfigurations.AsNoTracking().Where(c=>c.BranchId==work.BranchId&&c.IsCurrent&&c.IsActive&&c.SupplyGroup=="A"&&c.ItemId!=null&&c.UnitId!=null
            &&db.ItemLifecycleProfiles.Any(p=>p.ItemId==c.ItemId&&p.BranchId==work.BranchId&&p.Classification=="CONSUMABLE")).ToArrayAsync(ct);
        return(current,config.Select(c=>new AssessmentSource(null,null,c.ItemId!.Value,c.UnitId!.Value,c.PerGuestQuantity,capacity)).ToArray());
    }

    public async Task<RoomSupplyAssessmentDto> RoomAssessment(long workId,CancellationToken ct)
    {
        var work=await AssessmentWork(workId,ct);
        var saved=await Confirmation(workId,ct);
        if(saved!=null)return ReadConfirmation(saved).Result with{RowVersion=Convert.ToBase64String(work.RowVersion),ReadOnly=true,Historical=true};
        var posted=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="WorkItem"&&a.EntityKey==workId.ToString()
            &&(a.Action=="SUPPLIES_CONSUMED_AT_COMPLETION"||a.Action=="SUPPLIES_CONSUMED_AT_ACCEPTANCE")).OrderByDescending(a=>a.AuditId).FirstOrDefaultAsync(ct);
        if(posted?.AfterJson!=null){
            using var snapshot=JsonDocument.Parse(posted.AfterJson);
            if(snapshot.RootElement.TryGetProperty("result",out var result))
                return JsonSerializer.Deserialize<RoomSupplyAssessmentDto>(result.GetRawText())! with{RowVersion=Convert.ToBase64String(work.RowVersion)};
        }
        var historical=await HistoricalAssessment(work,ct);
        if(historical!=null)return historical;
        var (allocation,sources)=await AssessmentSources(work,ct);
        var lines=new List<RoomSupplyAssessmentLine>();
        foreach(var source in sources){
            var item=await db.InventoryItems.AsNoTracking().SingleAsync(i=>i.ItemId==source.ItemId,ct);
            var unit=await db.ReferenceValues.AsNoTracking().SingleAsync(u=>u.ReferenceId==source.UnitId&&u.Kind=="UOM",ct);
            var standard=SupplyMath.Required(source.Capacity,source.PerGuest);
            var consumed=await (from l in db.StaffSupplyLedgers join b in db.StaffSupplyBalances on l.BalanceId equals b.BalanceId
                where l.WorkId==workId&&l.Kind=="USE"&&b.ItemId==source.ItemId select (decimal?)l.Quantity).SumAsync(ct)??0;
            var required=Math.Max(0,standard-consumed);
            var available=source.Balance==null?0:await AvailableForReservation(source.Balance,0,ct);
            lines.Add(new(source.ItemId,item.Name,standard,null,required,available,Math.Max(0,required-available),source.UnitId,unit.Code,null));
        }
        var execution=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==workId&&e.EventType=="START").OrderByDescending(e=>e.WorkEventId).Select(e=>(long?)e.WorkEventId).FirstOrDefaultAsync(ct);
        return new(workId,work.RoomId!.Value,lines.Count>0&&lines.All(l=>l.RequiredQuantity==0),true,execution,Convert.ToBase64String(work.RowVersion),allocation?.AllocationId,lines);
    }

    private async Task<RoomSupplyAssessmentDto?> HistoricalAssessment(WorkItem work,CancellationToken ct)
    {
        var rows=await db.SupplyReservations.AsNoTracking().Where(r=>r.WorkId==work.WorkId&&r.Status!="RELEASED").ToArrayAsync(ct);
        var uses=await db.StaffSupplyLedgers.AsNoTracking().Where(l=>l.WorkId==work.WorkId&&l.Kind=="USE").ToArrayAsync(ct);
        if(!rows.Any(r=>r.Status=="CONSUMED")&&uses.Length==0)return null;
        Output.Require(HasValidHistoricalConsumption(rows,uses)&&uses.All(l=>l.Quantity>0)
            &&rows.All(r=>r.Capacity>0&&r.PerGuestQuantity>=0&&r.Quantity>=0),"MATERIAL_CONSUMPTION_INVALID");
        var ids=rows.Select(r=>r.BalanceId).Distinct().ToArray();
        var custody=await (from b in db.StaffSupplyBalances.AsNoTracking()
            join a in db.StaffSupplyAllocations.AsNoTracking() on b.AllocationId equals a.AllocationId
            where ids.Contains(b.BalanceId) select new{Balance=b,a.BranchId}).ToDictionaryAsync(x=>x.Balance.BalanceId,ct);
        Output.Require(rows.All(r=>r.RoomId==work.RoomId&&custody.TryGetValue(r.BalanceId,out var owner)
            &&owner.BranchId==work.BranchId),"SUPPLY_BRANCH_MISMATCH");
        var lines=new List<RoomSupplyAssessmentLine>();
        foreach(var group in rows.GroupBy(r=>custody[r.BalanceId].Balance.ItemId)){
            var first=group.First();var balance=custody[first.BalanceId].Balance;
            Output.Require(group.All(r=>r.Capacity==first.Capacity&&r.PerGuestQuantity==first.PerGuestQuantity
                &&custody[r.BalanceId].Balance.UnitId==balance.UnitId),"MATERIAL_CONSUMPTION_INVALID");
            var item=await db.InventoryItems.AsNoTracking().SingleAsync(i=>i.ItemId==group.Key,ct);
            var unit=await db.ReferenceValues.AsNoTracking().SingleAsync(u=>u.ReferenceId==balance.UnitId&&u.Kind=="UOM",ct);
            var reservationIds=group.Select(r=>r.ReservationId).ToHashSet();
            var consumed=uses.Where(l=>l.ReservationId.HasValue&&reservationIds.Contains(l.ReservationId.Value)).Sum(l=>l.Quantity);
            lines.Add(new(group.Key,item.Name,SupplyMath.Required(first.Capacity,first.PerGuestQuantity),
                null,consumed,0,0,balance.UnitId,unit.Code,null));
        }
        var allocationIds=custody.Values.Select(x=>x.Balance.AllocationId).Distinct().ToArray();
        var execution=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==work.WorkId&&e.EventType=="START")
            .OrderByDescending(e=>e.WorkEventId).Select(e=>(long?)e.WorkEventId).FirstOrDefaultAsync(ct);
        return new(work.WorkId,work.RoomId!.Value,true,true,execution,Convert.ToBase64String(work.RowVersion),
            allocationIds.Length==1?allocationIds[0]:null,lines.OrderBy(l=>l.ItemId).ToArray(),true);
    }

}
