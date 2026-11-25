using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class StaffSupplyService
{
    private async Task AllocateReusableForTask(StaffSupplyAllocation allocation,long workId,int capacity,Guid key,CancellationToken ct)
    {
        if(await db.AuditLogs.AnyAsync(a=>a.EntityType=="WorkItem"&&a.EntityKey==workId.ToString()
            &&a.Action=="REUSABLE_TASK_HANDOVER"&&a.OperationKey==key,ct))return;
        var configuration=await ReusableAllocationSnapshot(allocation,ct);
        var accounting=new ReusableAccounting(db,clock);
        // Clean custody remains owned until placement or return. Cover all other accepted,
        // unfinished rooms so the same reusable kit cannot cover two concurrent jobs.
        var otherRooms=await (from wa in db.WorkAssignments
            join w in db.WorkItems on wa.WorkId equals w.WorkId
            join room in db.Rooms on w.RoomId equals room.RoomId
            join type in db.RoomTypes on room.RoomTypeId equals type.RoomTypeId
            where wa.AssigneeUserId==allocation.UserId&&wa.AssignmentStatus=="ACTIVE"&&wa.AcceptedAt!=null
                &&w.WorkId!=workId&&w.BranchId==allocation.BranchId&&w.Kind=="CLEANING"
                &&(w.Status=="ASSIGNED"||w.Status=="IN_PROGRESS"||w.Status=="ON_HOLD")
            select new{w.WorkId,type.Capacity}).ToArrayAsync(ct);
        foreach(var c in configuration.Where(c=>c.SupplyGroup=="A")){
            Output.Require(c.ItemId!=null&&c.WarehouseId!=null&&c.PerGuestQuantity>0,"FILL_SUPPLY_CONFIGURATION_INCOMPLETE");
            decimal otherRequired=0;
            foreach(var other in otherRooms){
                var placed=await (from movement in db.ReusableMovements
                    join destination in db.ReusablePositions on movement.DestinationPositionId equals destination.PositionId
                    where movement.WorkId==other.WorkId&&movement.ItemId==c.ItemId&&destination.Status=="IN_USE"
                    select (decimal?)movement.Quantity).SumAsync(ct)??0;
                otherRequired+=Math.Max(0,SupplyMath.Required(other.Capacity,c.PerGuestQuantity)-placed);
            }
            var held=await db.ReusablePositions.Where(p=>p.BranchId==allocation.BranchId&&p.ItemId==c.ItemId
                &&p.UserId==allocation.UserId&&p.Status=="STAFF_HOLDING").SumAsync(p=>(decimal?)p.Quantity,ct)??0;
            var missing=Math.Max(0,SupplyMath.Required(capacity,c.PerGuestQuantity)+otherRequired-held);
            if(missing==0)continue;
            Output.Require(await db.Warehouses.AnyAsync(w=>w.WarehouseId==c.WarehouseId&&w.BranchId==allocation.BranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
            var source=await accounting.Position(allocation.BranchId,c.ItemId!.Value,"READY",c.WarehouseId,null,null,ct);
            var target=await accounting.Position(allocation.BranchId,c.ItemId.Value,"STAFF_HOLDING",null,allocation.UserId,null,ct);
            await accounting.Move(source,target,missing,actor.Id,key,"HANDOVER",$"Cấp vật tư cho công việc {workId}",ct,work:workId);
        }
        audit.Add("WorkItem",workId,"REUSABLE_TASK_HANDOVER",allocation.BranchId,
            after:new{allocation.AllocationId,allocation.UserId,capacity},key:key);
    }
    private async Task<SupplyConfiguration[]> ReusableAllocationSnapshot(StaffSupplyAllocation allocation,CancellationToken ct)
    {
        var json=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="StaffSupplyAllocation"&&a.EntityKey==allocation.AllocationId.ToString()&&a.Action=="REUSABLE_CONFIGURATION_SNAPSHOT")
            .OrderBy(a=>a.AuditId).Select(a=>a.AfterJson).FirstOrDefaultAsync(ct);
        if(json!=null)return System.Text.Json.JsonSerializer.Deserialize<SupplyConfiguration[]>(json,new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
        // Recover from the immutable pre-cutover shift snapshot, never today's changed configuration.
        var old=await db.StaffSupplyBalances.AsNoTracking().Where(b=>b.AllocationId==allocation.AllocationId&&db.ItemLifecycleProfiles.Any(p=>p.BranchId==allocation.BranchId&&p.ItemId==b.ItemId&&p.Classification=="REUSABLE")).ToArrayAsync(ct);
        var result=new List<SupplyConfiguration>();
        foreach(var balance in old){
            var c=await db.SupplyConfigurations.AsNoTracking().SingleAsync(c=>c.ConfigurationId==balance.ConfigurationId,ct);
            result.Add(c);
        }
        audit.Add("StaffSupplyAllocation",allocation.AllocationId,"REUSABLE_CONFIGURATION_SNAPSHOT",allocation.BranchId,after:result);
        return result.ToArray();
    }
    private Task<SupplyConfiguration[]> ReusableShiftConfiguration(long branch,CancellationToken ct)=>db.SupplyConfigurations.AsNoTracking()
        .Where(c=>c.BranchId==branch&&c.IsCurrent&&c.IsActive&&c.SupplyGroup=="A"&&c.PerGuestQuantity>0&&c.ItemId!=null
            &&db.ItemLifecycleProfiles.Any(p=>p.BranchId==branch&&p.ItemId==c.ItemId&&p.Classification=="REUSABLE"))
        .OrderBy(c=>c.ItemId).ToArrayAsync(ct);

    private async Task<bool> ReusableShiftAvailable(SupplyConfiguration configuration,CancellationToken ct)
    {
        if(configuration.WarehouseId==null||!await db.Warehouses.AnyAsync(w=>w.WarehouseId==configuration.WarehouseId&&w.BranchId==configuration.BranchId&&w.IsActive,ct))return false;
        return (await db.ReusablePositions.Where(p=>p.BranchId==configuration.BranchId&&p.ItemId==configuration.ItemId&&p.WarehouseId==configuration.WarehouseId&&p.Status=="READY")
            .SumAsync(p=>(decimal?)p.Quantity,ct)??0)>=configuration.InitialQuantity;
    }
    private async Task AllocateReusable(StaffSupplyAllocation allocation,SupplyConfiguration[] configuration,Guid key,CancellationToken ct)
    {
        var accounting=new ReusableAccounting(db,clock);
        foreach(var c in configuration){
            var source=await accounting.Position(allocation.BranchId,c.ItemId!.Value,"READY",c.WarehouseId,null,null,ct);
            var target=await accounting.Position(allocation.BranchId,c.ItemId.Value,"STAFF_HOLDING",null,allocation.UserId,null,ct);
            await accounting.Move(source,target,c.InitialQuantity,actor.Id,key,"HANDOVER",$"Cấp vật tư tái sử dụng đầu ca {allocation.AssignmentId}",ct);
        }
        if(configuration.Length>0)audit.Add("StaffSupplyAllocation",allocation.AllocationId,"REUSABLE_INITIAL_HANDOVER",allocation.BranchId,
            after:new{allocation.AssignmentId,allocation.UserId,items=configuration.Select(c=>new{c.ConfigurationId,c.ItemId,c.WarehouseId,c.InitialQuantity})},key:key);
    }
    private async Task ReturnReusable(StaffSupplyAllocation allocation,Guid key,CancellationToken ct)
    {
        var positions=await db.ReusablePositions.Where(p=>p.BranchId==allocation.BranchId&&p.UserId==allocation.UserId&&p.Quantity>0&&
            (p.Status=="STAFF_HOLDING"||p.Status=="DIRTY"||p.Status=="DAMAGED")).OrderBy(p=>p.PositionId).ToArrayAsync(ct);
        if(positions.Length==0)return;
        var warehouses=await db.Warehouses.Where(w=>w.BranchId==allocation.BranchId&&w.IsActive).Select(w=>w.WarehouseId).ToArrayAsync(ct);
        Output.Require(warehouses.Length==1,"BRANCH_REQUIRES_ONE_ACTIVE_WAREHOUSE");
        var accounting=new ReusableAccounting(db,clock);
        foreach(var source in positions){
            // STAFF_HOLDING means clean, unused custody. Dirty/damaged custody is never made ready by return.
            var target=await accounting.Position(allocation.BranchId,source.ItemId,source.Status=="STAFF_HOLDING"?"READY":source.Status,warehouses[0],null,null,ct);
            await accounting.Move(source,target,source.Quantity,actor.Id,Guid.NewGuid(),"HANDOVER",$"Hoàn trả vật tư tái sử dụng cuối ca {allocation.AssignmentId}",ct);
        }
        audit.Add("StaffSupplyAllocation",allocation.AllocationId,"REUSABLE_RETURN_HANDOVER",allocation.BranchId,
            after:new{allocation.AssignmentId,allocation.UserId,positions=positions.Select(p=>p.PositionId)},key:key);
    }
}
