using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class RequestWorkflowService
{
    public async Task<object> RefillOptions(long branch,CancellationToken ct)
    {
        await access.Require("IR_CREATE",branch,true,ct);
        Output.Require(await access.HasRole("HK_STAFF",branch,ct),"REFILL_REQUESTER_REQUIRED",403);
        var allocations=await db.StaffSupplyAllocations.AsNoTracking().Where(a=>a.UserId==actor.Id&&a.BranchId==branch&&a.Status!="CLOSED"
            &&db.WorkdayAssignments.Any(w=>w.AssignmentId==a.AssignmentId&&w.Status=="WORKING"&&w.StartedAt!=null&&w.EndedAt==null)).ToArrayAsync(ct);
        Output.Require(allocations.Length==1,"ACTIVE_SHIFT_REQUIRED",403);
        var allocation=allocations[0];await ActiveAllocation(allocation,ct);
        var items=await (from b in db.StaffSupplyBalances.AsNoTracking() join i in db.InventoryItems on b.ItemId equals i.ItemId
            join unit in db.ReferenceValues on b.UnitId equals unit.ReferenceId
            where b.AllocationId==allocation.AllocationId&&i.IsActive
                &&db.ItemLifecycleProfiles.Any(p=>p.ItemId==i.ItemId&&p.BranchId==branch&&p.Classification=="CONSUMABLE")
            orderby i.Name select new {i.ItemId,i.Name,unit=unit.Name}).ToArrayAsync(ct);
        var reusable=await supplies.ReusableAllocationSnapshot(allocation,ct);
        var reusableIds=reusable.Where(c=>c.ItemId!=null).Select(c=>c.ItemId!.Value).ToArray();
        var reusableItems=await (from i in db.InventoryItems.AsNoTracking() join unit in db.ReferenceValues on i.BaseUnitId equals unit.ReferenceId
            where reusableIds.Contains(i.ItemId)&&i.IsActive orderby i.Name select new{i.ItemId,i.Name,unit=unit.Name}).ToArrayAsync(ct);
        items=items.Concat(reusableItems).DistinctBy(i=>i.ItemId).ToArray();
        var assignment=await db.WorkdayAssignments.AsNoTracking().SingleAsync(a=>a.AssignmentId==allocation.AssignmentId,ct);
        var shift=await db.WorkdayShifts.AsNoTracking().SingleAsync(s=>s.ShiftId==assignment.ShiftId,ct);
        return new {allocationId=allocation.AllocationId,shiftName=shift.Name,workDate=assignment.WorkDate,startedAt=assignment.StartedAt,items};
    }
}
