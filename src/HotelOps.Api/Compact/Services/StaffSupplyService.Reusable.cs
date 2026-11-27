using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class StaffSupplyService
{
    private async Task AllocateReusableForTask(StaffSupplyAllocation allocation,long workId,int capacity,Guid key,CancellationToken ct)
    {
        // Work identity, not the caller's retry key, owns the one placement.
        if(await db.AuditLogs.AnyAsync(a=>a.EntityType=="WorkItem"&&a.EntityKey==workId.ToString()
            &&a.Action=="REUSABLE_TASK_PLACED",ct))return;
        var work=await db.WorkItems.SingleAsync(w=>w.WorkId==workId&&w.BranchId==allocation.BranchId,ct);
        Output.Require(work.RoomId!=null,"ROOM_REQUIRED");
        var configuration=await ReusableAllocationSnapshot(allocation,ct);
        var accounting=new ReusableAccounting(db,clock);
        var placementPlan=new List<(long ItemId,decimal Quantity)>();
        foreach(var c in configuration.Where(c=>c.SupplyGroup=="A")){
            Output.Require(c.ItemId!=null&&c.WarehouseId!=null&&c.PerGuestQuantity>0,"FILL_SUPPLY_CONFIGURATION_INCOMPLETE");
            var required=SupplyMath.Required(capacity,c.PerGuestQuantity);
            // Preserve a manual placement made before automatic placement was introduced.
            var placed=await (from m in db.ReusableMovements join p in db.ReusablePositions on m.DestinationPositionId equals p.PositionId
                where m.WorkId==workId&&m.ItemId==c.ItemId&&p.RoomId==work.RoomId&&p.Status=="IN_USE"
                select (decimal?)m.Quantity).SumAsync(ct)??0;
            var remaining=Math.Max(0,required-placed);
            if(remaining==0)continue;
            var held=await db.ReusablePositions.AsNoTracking().Where(p=>p.BranchId==allocation.BranchId
                &&p.ItemId==c.ItemId&&p.UserId==allocation.UserId&&p.Status=="STAFF_HOLDING")
                .Select(p=>(decimal?)p.Quantity).SingleOrDefaultAsync(ct)??0;
            Output.Require(held>=remaining,"STAFF_SUPPLIES_INSUFFICIENT",409);
            Output.Require(await ReusableMissingCustody.RoomUnavailable(db,allocation.BranchId,work.RoomId!.Value,c.ItemId!.Value,ct)==0,
                "REUSABLE_ROOM_DISCREPANCY",409);
            Output.Require(!await ReusableShelfLifePolicy.Overdue(db,allocation.BranchId,c.ItemId.Value,Now,ct),
                "REUSABLE_SHELF_REVIEW_REQUIRED",409);
            placementPlan.Add((c.ItemId!.Value,remaining));
        }
        // Recovery can catch shortages inside its outer transaction. Check every line
        // before creating positions or moving any part of the kit.
        foreach(var entry in placementPlan){
            var remaining=entry.Quantity;
            var held=await accounting.Position(allocation.BranchId,entry.ItemId,"STAFF_HOLDING",null,allocation.UserId,null,ct);
            var room=await accounting.Position(allocation.BranchId,entry.ItemId,"IN_USE",null,null,work.RoomId,ct);
            var dirty=await accounting.Position(allocation.BranchId,entry.ItemId,"DIRTY",null,allocation.UserId,null,ct);
            var collected=Math.Min(room.Quantity,remaining);
            if(collected>0)await accounting.Move(room,dirty,collected,allocation.UserId,Guid.NewGuid(),"REPLACEMENT",
                $"Thu đồ đã dùng khi nhận công việc {workId}",ct,work:workId);
            await accounting.Move(held,room,remaining,allocation.UserId,Guid.NewGuid(),"MOVE",
                $"Đặt bộ sạch khi nhận công việc {workId}",ct,work:workId);
        }
        var snapshot=new{allocation.AllocationId,allocation.UserId,capacity};
        if(actor.Request==null)db.AuditLogs.Add(new AuditLog{ActorUserId=null,BranchId=allocation.BranchId,EntityType="WorkItem",EntityKey=workId.ToString(),
            Action="REUSABLE_TASK_PLACED",OperationKey=key,OccurredAt=Now,AfterJson=System.Text.Json.JsonSerializer.Serialize(snapshot)});
        else audit.Add("WorkItem",workId,"REUSABLE_TASK_PLACED",allocation.BranchId,after:snapshot,key:key);
        await db.SaveChangesAsync(ct);
    }
    internal async Task<SupplyConfiguration[]> ReusableAllocationSnapshot(StaffSupplyAllocation allocation,CancellationToken ct)
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
        .Where(c=>c.BranchId==branch&&c.IsCurrent&&c.IsActive&&c.SupplyGroup=="A"&&c.ItemId!=null
            &&db.ItemLifecycleProfiles.Any(p=>p.BranchId==branch&&p.ItemId==c.ItemId&&p.Classification=="REUSABLE"))
        .OrderBy(c=>c.ItemId).ToArrayAsync(ct);

    private async Task<bool> ReusableShiftAvailable(SupplyConfiguration configuration,CancellationToken ct)
    {
        if(configuration.ItemId==null||configuration.UnitId==null||configuration.InitialQuantity<=0||configuration.PerGuestQuantity<=0
            ||!SupplyMath.ValidQuantity(configuration.InitialQuantity)||!SupplyMath.ValidQuantity(configuration.PerGuestQuantity)
            ||decimal.Truncate(configuration.InitialQuantity)!=configuration.InitialQuantity||decimal.Truncate(configuration.PerGuestQuantity)!=configuration.PerGuestQuantity)return false;
        if(!await db.InventoryItems.AnyAsync(i=>i.ItemId==configuration.ItemId&&i.IsActive&&i.BaseUnitId==configuration.UnitId,ct))return false;
        if(await ReusableShelfLifePolicy.Overdue(db,configuration.BranchId,configuration.ItemId.Value,Now,ct))return false;
        if(configuration.WarehouseId==null||!await db.Warehouses.AnyAsync(w=>w.WarehouseId==configuration.WarehouseId&&w.BranchId==configuration.BranchId&&w.IsActive,ct))return false;
        return (await db.ReusablePositions.Where(p=>p.BranchId==configuration.BranchId&&p.ItemId==configuration.ItemId&&p.WarehouseId==configuration.WarehouseId&&p.Status=="READY")
            .SumAsync(p=>(decimal?)p.Quantity,ct)??0)>=configuration.InitialQuantity;
    }
    private async Task AllocateReusable(StaffSupplyAllocation allocation,SupplyConfiguration[] configuration,Guid key,CancellationToken ct,long? authorizingUserId=null)
    {
        var accounting=new ReusableAccounting(db,clock);
        foreach(var c in configuration){
            var source=await accounting.Position(allocation.BranchId,c.ItemId!.Value,"READY",c.WarehouseId,null,null,ct);
            var target=await accounting.Position(allocation.BranchId,c.ItemId.Value,"STAFF_HOLDING",null,allocation.UserId,null,ct);
            await accounting.Move(source,target,c.InitialQuantity,authorizingUserId??actor.Id,key,"HANDOVER",$"Cấp vật tư tái sử dụng đầu ca {allocation.AssignmentId}",ct);
        }
        if(configuration.Length>0)SystemSupplyAudit(allocation,"REUSABLE_INITIAL_HANDOVER",key,new{allocation.AssignmentId,allocation.UserId,items=configuration.Select(c=>new{c.ConfigurationId,c.ItemId,c.WarehouseId,c.InitialQuantity})});
    }
    private async Task ReturnReusable(StaffSupplyAllocation allocation,Guid key,CancellationToken ct,long? authorizingUserId=null)
    {
        // Custody is pooled per user, not allocation. Never sweep a later shift's kit
        // while reconciling an older allocation; the latest allocation owns that sweep.
        if(await db.StaffSupplyAllocations.AnyAsync(a=>a.BranchId==allocation.BranchId&&a.UserId==allocation.UserId
            &&a.AllocationId>allocation.AllocationId,ct)){
            SystemSupplyAudit(allocation,"REUSABLE_RETURN_DEFERRED",key,new{reason="Later allocation owns pooled staff custody"});
            return;
        }
        var author=authorizingUserId??actor.Id;
        var positions=await db.ReusablePositions.Where(p=>p.BranchId==allocation.BranchId&&p.UserId==allocation.UserId&&p.Quantity>0&&
            (p.Status=="STAFF_HOLDING"||p.Status=="DIRTY"||p.Status=="DAMAGED")).OrderBy(p=>p.PositionId).ToArrayAsync(ct);
        if(positions.Length==0)return;
        var warehouses=await db.Warehouses.Where(w=>w.BranchId==allocation.BranchId&&w.IsActive).Select(w=>w.WarehouseId).ToArrayAsync(ct);
        Output.Require(warehouses.Length==1,"BRANCH_REQUIRES_ONE_ACTIVE_WAREHOUSE");
        var accounting=new ReusableAccounting(db,clock);
        foreach(var source in positions){
            // Unverified missing returns are not physical stock available to send to laundry.
            // Keep custody intact until the independent review resolves the report.
            if(source.Status=="DIRTY"&&await ReusableMissingCustody.HasPending(db,source.PositionId,ct)){
                SystemSupplyAudit(allocation,"REUSABLE_RETURN_AWAITING_VERIFICATION",key,new{source.PositionId,source.Quantity});
                continue;
            }
            // The proposal follows the damaged units to warehouse custody; its decision,
            // author and quantity remain unchanged. The caller holds the supply lock and transaction.
            var proposals=await db.ReusableWriteOffs.Where(w=>w.PositionId==source.PositionId&&w.Status=="PENDING").ToArrayAsync(ct);
            var held=proposals.Sum(w=>w.Quantity);
            Output.Require(held<=source.Quantity,"REUSABLE_AVAILABLE_INSUFFICIENT");
            Output.Require(held==0||source.Status=="DAMAGED","DAMAGED_ITEMS_REQUIRED");
            var returning=source.Quantity;
            var target=await accounting.Position(allocation.BranchId,source.ItemId,source.Status=="STAFF_HOLDING"?"READY":source.Status,warehouses[0],null,null,ct);
            foreach(var proposal in proposals){
                proposal.PositionId=target.PositionId;
                SystemSupplyAudit(allocation,"REUSABLE_WRITE_OFF_RELOCATED",key,
                    new{proposal.WriteOffId,sourcePositionId=source.PositionId,destinationPositionId=target.PositionId,proposal.Quantity});
            }
            // Accounting checks the persisted reservation, so relocate it before moving stock.
            // Both saves commit or roll back together with the shift closure.
            if(proposals.Length>0)await db.SaveChangesAsync(ct);
            await accounting.Move(source,target,returning,author,Guid.NewGuid(),"HANDOVER",$"Hoàn trả vật tư tái sử dụng cuối ca {allocation.AssignmentId}",ct);
        }
        SystemSupplyAudit(allocation,"REUSABLE_RETURN_HANDOVER",key,
            new{allocation.AssignmentId,allocation.UserId,authorizingUserId,positions=positions.Select(p=>p.PositionId)});
    }
}
