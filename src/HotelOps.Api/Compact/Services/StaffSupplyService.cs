using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,DocumentService documents,LowStockService lowStock)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    // One short application lock gives all writers a consistent lock order, including reassignments.
    internal static Task Lock(HotelOpsCompactDbContext db,CancellationToken ct)=>db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.StaffSupplies',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51500,'Supply lock unavailable.',1;",ct);

    public async Task<IReadOnlyList<StaffSupplyAllocationDto>> List(long branch,long? userId,CancellationToken ct)
    {
        var branchRead=await access.Allows("STAFF_SUPPLY_VIEW",branch,ct:ct);
        if(!branchRead){await access.Require("STAFF_SUPPLY_VIEW",branch,true,ct);Output.Require(userId==null||userId==actor.Id,"SUPPLY_OWNER_REQUIRED",403);userId=actor.Id;}
        var rows=await db.StaffSupplyAllocations.AsNoTracking().Where(x=>x.BranchId==branch&&(userId==null||x.UserId==userId)).OrderByDescending(x=>x.AllocationId).Take(100).ToArrayAsync(ct);
        var result=new List<StaffSupplyAllocationDto>();foreach(var row in rows)result.Add(await Dto(row,ct));return result;
    }
    public async Task<IReadOnlyList<SupplyLedgerDto>> History(long id,CancellationToken ct)
    {
        var allocation=await db.StaffSupplyAllocations.AsNoTracking().SingleOrDefaultAsync(x=>x.AllocationId==id,ct)??throw new ApiException(404,"SUPPLY_ALLOCATION_NOT_FOUND");
        await access.Require("STAFF_SUPPLY_VIEW",allocation.BranchId,allocation.UserId==actor.Id,ct);
        return await (from entry in db.StaffSupplyLedgers.AsNoTracking() join balance in db.StaffSupplyBalances on entry.BalanceId equals balance.BalanceId
            where balance.AllocationId==id orderby entry.LedgerId descending
            select new SupplyLedgerDto(entry.LedgerId,balance.ItemId,entry.Kind,entry.Quantity,entry.WorkId,entry.ReservationId,entry.DocumentLineId,entry.ActorUserId,entry.OccurredAt)).Take(1000).ToArrayAsync(ct);
    }

    public async Task<object> Document(long id,CancellationToken ct)
    {
        var link=await db.SupplyStockDocuments.AsNoTracking().SingleOrDefaultAsync(x=>x.DocumentId==id,ct)??throw new ApiException(404,"SUPPLY_DOCUMENT_NOT_FOUND");
        var a=await db.StaffSupplyAllocations.AsNoTracking().SingleAsync(x=>x.AllocationId==link.AllocationId,ct);
        await access.Require("STAFF_SUPPLY_VIEW",a.BranchId,a.UserId==actor.Id,ct);
        var document=await db.InventoryDocuments.AsNoTracking().SingleAsync(x=>x.DocumentId==id,ct);
        var lines=await db.InventoryDocumentLines.AsNoTracking().Where(x=>x.DocumentId==id).OrderBy(x=>x.LineNo).ToArrayAsync(ct);
        return new{document=Output.Record(document),lines=lines.Select(Output.Record).ToArray(),link.Purpose,a.AssignmentId,a.UserId};
    }

    // Call immediately AFTER workday start is saved, in the same Atomic transaction.
    internal Task<StaffSupplyAllocationDto> Start(long assignmentId,CancellationToken ct)=>atomic.Run(async()=>{
        await Lock(db,ct);
        var assignment=await db.WorkdayAssignments.SingleOrDefaultAsync(x=>x.AssignmentId==assignmentId,ct)??throw new ApiException(404,"SHIFT_NOT_FOUND");
        var shift=await db.WorkdayShifts.SingleAsync(x=>x.ShiftId==assignment.ShiftId,ct);
        Output.Require(assignment.UserId==actor.Id,"SHIFT_OWNER_REQUIRED",403);
        await access.Require("ATTENDANCE_RECORD",shift.BranchId,true,ct);
        await ActiveShift(assignment,shift,ct);
        var existing=await db.StaffSupplyAllocations.SingleOrDefaultAsync(x=>x.AssignmentId==assignmentId,ct);
        if(existing!=null)return await Dto(existing,ct);
        return await Allocate(assignment,shift,false,ct);
    },ct);

    // Caller holds the supply lock and outer serializable transaction. Recovery must roll back
    // the allocation AND stock issue if any item or the ensuing room reservation is unavailable.
    private async Task<StaffSupplyAllocationDto> Allocate(WorkdayAssignment assignment,WorkdayShift shift,bool requireFull,CancellationToken ct)
    {
        var config=await db.SupplyConfigurations.AsNoTracking().Where(x=>x.BranchId==shift.BranchId&&x.IsCurrent&&x.IsActive&&x.SupplyGroup=="A").OrderBy(x=>x.ItemId).ToArrayAsync(ct);
        var allocation=new StaffSupplyAllocation{BranchId=shift.BranchId,UserId=assignment.UserId,AssignmentId=assignment.AssignmentId,CreatedAt=Now};
        db.StaffSupplyAllocations.Add(allocation);await db.SaveChangesAsync(ct);
        var balances=new List<StaffSupplyBalance>();
        foreach(var c in config){
            Output.Require(c.ItemId!=null&&c.WarehouseId!=null&&c.UnitId!=null&&c.InitialQuantity>0&&c.PerGuestQuantity>0,"FILL_SUPPLY_CONFIGURATION_INCOMPLETE");
            balances.Add(new StaffSupplyBalance{AllocationId=allocation.AllocationId,ConfigurationId=c.ConfigurationId,ItemId=c.ItemId!.Value,WarehouseId=c.WarehouseId!.Value,UnitId=c.UnitId!.Value,InitialTarget=c.InitialQuantity,PerGuestQuantity=c.PerGuestQuantity});
        }
        db.StaffSupplyBalances.AddRange(balances);await db.SaveChangesAsync(ct);
        var shortages=new List<long>();
        foreach(var b in balances)if(!await WarehouseAvailable(b,ct)||await Stock(b,ct)<b.InitialTarget)shortages.Add(b.ItemId);
        if(config.Length==0||shortages.Count>0){
            if(requireFull)throw new ApiException(409,config.Length==0?"SUPPLY_CONFIGURATION_REQUIRED":"STOCK_INSUFFICIENT");
            allocation.ShortageReason=config.Length==0?"SUPPLY_CONFIGURATION_REQUIRED":"STOCK_INSUFFICIENT:"+string.Join(',',shortages);
            audit.Add("StaffSupplyAllocation",allocation.AllocationId,"INITIAL_SHORT",allocation.BranchId,after:new{allocation.ShortageReason});
            await db.SaveChangesAsync(ct);await lowStock.InitialShortage(allocation,ct);
        }else{
            var key=Guid.NewGuid();
            var lines=await documents.PostStaffSupply(allocation,balances.Select(b=>(b,b.InitialTarget)).ToArray(),"INITIAL",key,null,ct);
            foreach(var b in balances){b.InitialIssued=b.InitialTarget;Ledger(b,"INITIAL",b.InitialTarget,key,documentLineId:lines[b.BalanceId]);}
            allocation.Status="OPEN";
            audit.Add("StaffSupplyAllocation",allocation.AllocationId,"INITIAL_ISSUED",allocation.BranchId,key:key);
        }
        await db.SaveChangesAsync(ct);return await Dto(allocation,ct);
    }

    // Internal request processor must validate PENDING_STOREKEEPER and approved quantities first.
    // This method rechecks role, active shift, stock and the immutable request idempotency link.
    internal Task<StaffSupplyAllocationDto> Refill(long allocationId,long requestWorkId,IReadOnlyList<SupplyIssueQuantity> requested,Guid key,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var a=await LoadAllocation(allocationId,ct);
        await access.RequireRolePermission("STOREKEEPER","MR_FULFILL",a.BranchId,ct);
        Output.Require(await db.WorkItems.AnyAsync(x=>x.WorkId==requestWorkId&&x.BranchId==a.BranchId&&x.Kind=="INTERNAL_REQUEST"&&x.CreatedBy==a.UserId,ct),"SUPPLY_REQUEST_MISMATCH");
        if(await db.SupplyStockDocuments.AnyAsync(x=>x.AllocationId==a.AllocationId&&x.RequestWorkId==requestWorkId&&x.Purpose=="ADDITIONAL",ct))return await Dto(a,ct);
        await AllocationActive(a,ct);
        Output.Require(requested.Count is >0 and <=100&&requested.Select(x=>x.ItemId).Distinct().Count()==requested.Count&&requested.All(x=>x.Quantity>0&&SupplyMath.ValidQuantity(x.Quantity)),"INVALID_SUPPLY_QUANTITIES",400);
        var balances=await db.StaffSupplyBalances.Where(x=>x.AllocationId==a.AllocationId).ToArrayAsync(ct);
        var selected=new List<(StaffSupplyBalance Balance,decimal Quantity)>();
        foreach(var request in requested){
            var b=balances.SingleOrDefault(x=>x.ItemId==request.ItemId)??throw new ApiException(409,"SUPPLY_ITEM_NOT_IN_SHIFT_SNAPSHOT");
            Output.Require(await WarehouseAvailable(b,ct),"WAREHOUSE_UNAVAILABLE");Output.Require(await Stock(b,ct)>=request.Quantity,"STOCK_INSUFFICIENT");
            selected.Add((b,request.Quantity));
        }
        var lines=await documents.PostStaffSupply(a,selected,"ADDITIONAL",key,requestWorkId,ct);
        foreach(var (b,quantity) in selected){b.AdditionalIssued+=quantity;Ledger(b,"ADDITIONAL",quantity,key,documentLineId:lines[b.BalanceId],workId:requestWorkId);}
        a.Status=balances.All(b=>SupplyMath.Remaining(b)>=b.InitialTarget)?"OPEN":a.Status;
        if(a.Status=="OPEN")a.ShortageReason=null;
        audit.Add("StaffSupplyAllocation",a.AllocationId,"REFILLED",a.BranchId,key:key);await db.SaveChangesAsync(ct);return await Dto(a,ct);
    },ct);

    internal async Task<string?> EndBlocker(long assignmentId,CancellationToken ct)
    {
        var shift=await (from assignment in db.WorkdayAssignments.AsNoTracking()
            join template in db.WorkdayShifts on assignment.ShiftId equals template.ShiftId
            join role in db.Roles on template.RoleId equals role.RoleId
            where assignment.AssignmentId==assignmentId select new {assignment.UserId,assignment.Status,template.BranchId,role.Code}).SingleAsync(ct);
        if(shift.Status!="WORKING")return "SHIFT_NOT_WORKING";
        if(shift.Code!="HK_STAFF")return null;
        if(await (from assignment in db.WorkAssignments.AsNoTracking() join work in db.WorkItems on assignment.WorkId equals work.WorkId
            where assignment.AssigneeUserId==shift.UserId&&assignment.AssignmentStatus=="ACTIVE"&&work.BranchId==shift.BranchId
                &&!WorkRules.Terminal.Contains(work.Status)&&work.Status!="PENDING_INSPECTION"&&work.Status!="PENDING_REINSPECTION"&&work.Status!="PENDING_VERIFICATION"
            select work.WorkId).AnyAsync(ct))return "SHIFT_WORK_OPEN";
        var allocationIds=db.StaffSupplyAllocations.Where(a=>a.AssignmentId==assignmentId).Select(a=>a.AllocationId);
        var balances=db.StaffSupplyBalances.Where(b=>allocationIds.Contains(b.AllocationId));
        if(await balances.AnyAsync(b=>b.Reserved>0,ct)||await db.SupplyReservations.AnyAsync(r=>r.Status=="RESERVED"&&balances.Any(b=>b.BalanceId==r.BalanceId),ct))return "SUPPLY_RESERVATIONS_OPEN";
        if(await db.RequestWorkflows.AnyAsync(r=>r.AllocationId!=null&&allocationIds.Contains(r.AllocationId.Value)
            &&r.RequestType==RequestWorkflowService.RefillType&&r.Status!="FULFILLED"&&r.Status!="COMPLETED"&&r.Status!="REJECTED"&&r.Status!="CANCELLED",ct))return "REFILL_REQUEST_OPEN";
        return null;
    }

    // Call BEFORE ending workday; recheck under the same stock lock as refill/consumption.
    internal Task<StaffSupplyAllocationDto?> End(long assignmentId,Guid key,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var blocker=await EndBlocker(assignmentId,ct);
        Output.Require(blocker==null,blocker??"SHIFT_WORK_OPEN");
        var a=await db.StaffSupplyAllocations.SingleOrDefaultAsync(x=>x.AssignmentId==assignmentId,ct);
        if(a==null)return null;
        Output.Require(a.UserId==actor.Id,"SUPPLY_OWNER_REQUIRED",403);await access.Require("ATTENDANCE_RECORD",a.BranchId,true,ct);
        if(a.Status=="CLOSED")return await Dto(a,ct);
        var balances=await db.StaffSupplyBalances.Where(x=>x.AllocationId==a.AllocationId).ToArrayAsync(ct);
        Output.Require(balances.All(x=>x.Reserved==0)&&!await db.SupplyReservations.AnyAsync(r=>r.Status=="RESERVED"&&db.StaffSupplyBalances.Any(b=>b.BalanceId==r.BalanceId&&b.AllocationId==a.AllocationId),ct),"SUPPLY_RESERVATIONS_OPEN");
        Output.Require(await db.WorkdayAssignments.AnyAsync(x=>x.AssignmentId==assignmentId&&x.UserId==a.UserId&&x.Status=="WORKING",ct),"ACTIVE_SHIFT_REQUIRED");
        Output.Require(!await (from assignment in db.WorkAssignments join work in db.WorkItems on assignment.WorkId equals work.WorkId where assignment.AssigneeUserId==a.UserId&&assignment.AssignmentStatus=="ACTIVE"&&work.BranchId==a.BranchId&&(work.Kind=="CLEANING"||work.Kind=="REWORK")&&!WorkRules.Terminal.Contains(work.Status)&&work.Status!="PENDING_INSPECTION"&&work.Status!="PENDING_REINSPECTION" select work.WorkId).AnyAsync(ct),"CLEANING_WORK_OPEN");
        var remaining=balances.Where(x=>SupplyMath.Remaining(x)>0).Select(x=>(x,SupplyMath.Remaining(x))).ToArray();
        if(remaining.Length>0){var lines=await documents.PostStaffSupply(a,remaining,"RETURN",key,null,ct);foreach(var (b,quantity) in remaining){b.Returned+=quantity;Ledger(b,"RETURN",quantity,key,documentLineId:lines[b.BalanceId]);}}
        a.Status="CLOSED";a.ClosedAt=Now;a.ShortageReason=null;
        audit.Add("StaffSupplyAllocation",a.AllocationId,"RETURNED_CLOSED",a.BranchId,key:key);await db.SaveChangesAsync(ct);return await Dto(a,ct);
    },ct);

    private async Task ActiveShift(WorkdayAssignment assignment,WorkdayShift shift,CancellationToken ct,bool allowFinishing=false)
    {
        await access.Eligible(assignment.UserId,shift.BranchId,ct);
        Output.Require(await db.Roles.AnyAsync(r=>r.RoleId==shift.RoleId&&r.Code=="HK_STAFF"&&r.IsActive,ct),"HOUSEKEEPING_SHIFT_REQUIRED");
        Output.Require(assignment.Status=="WORKING"&&assignment.StartedAt!=null&&assignment.EndedAt==null&&shift.IsActive,"ACTIVE_SHIFT_REQUIRED");
        var now=Now;
        Output.Require(await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId where ur.UserId==assignment.UserId&&ur.BranchId==shift.BranchId&&ur.RevokedAt==null&&ur.AssignedAt<=now&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&role.Code=="HK_STAFF"&&role.IsActive select ur.UserRoleId).AnyAsync(ct),"HK_STAFF_REQUIRED",403);
        var zone=TimeZoneInfo.FindSystemTimeZoneById(await db.Branches.Where(x=>x.BranchId==shift.BranchId).Select(x=>x.Timezone).SingleAsync(ct));
        var start=TimeZoneInfo.ConvertTimeToUtc(assignment.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.StartMinute),zone);
        var end=TimeZoneInfo.ConvertTimeToUtc(assignment.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.EndMinute),zone);
        Output.Require(now>=start&&(allowFinishing||now<end),"ACTIVE_SHIFT_REQUIRED");
    }
    private async Task AllocationActive(StaffSupplyAllocation a,CancellationToken ct,bool allowFinishing=false){Output.Require(a.Status!="CLOSED","SUPPLY_ALLOCATION_CLOSED");var assignment=await db.WorkdayAssignments.SingleAsync(x=>x.AssignmentId==a.AssignmentId,ct);await ActiveShift(assignment,await db.WorkdayShifts.SingleAsync(x=>x.ShiftId==assignment.ShiftId,ct),ct,allowFinishing);}
    private async Task<StaffSupplyAllocation> LoadAllocation(long id,CancellationToken ct)=>await db.StaffSupplyAllocations.SingleOrDefaultAsync(x=>x.AllocationId==id,ct)??throw new ApiException(404,"SUPPLY_ALLOCATION_NOT_FOUND");
    private Task<bool> WarehouseAvailable(StaffSupplyBalance b,CancellationToken ct)=>db.Warehouses.AnyAsync(x=>x.WarehouseId==b.WarehouseId&&x.IsActive,ct);
    private async Task<decimal> Stock(StaffSupplyBalance b,CancellationToken ct)=>await db.StockMovements.Where(x=>x.WarehouseId==b.WarehouseId&&x.ItemId==b.ItemId).SumAsync(x=>(decimal?)x.SignedBaseQty,ct)??0;
    private void Ledger(StaffSupplyBalance b,string kind,decimal quantity,Guid key,long? workId=null,long? reservationId=null,long? documentLineId=null)=>db.StaffSupplyLedgers.Add(new StaffSupplyLedger{BalanceId=b.BalanceId,Kind=kind,Quantity=quantity,OperationKey=key,WorkId=workId,ReservationId=reservationId,DocumentLineId=documentLineId,ActorUserId=actor.Id,OccurredAt=Now});
    private async Task<StaffSupplyAllocationDto> Dto(StaffSupplyAllocation a,CancellationToken ct){var rows=await (from b in db.StaffSupplyBalances.AsNoTracking() join item in db.InventoryItems on b.ItemId equals item.ItemId where b.AllocationId==a.AllocationId orderby item.Name select new{b,item.Code,item.Name}).ToArrayAsync(ct);return new(a.AllocationId,a.BranchId,a.UserId,a.AssignmentId,a.Status,a.ShortageReason,a.CreatedAt,a.ClosedAt,Convert.ToBase64String(a.RowVersion),rows.Select(x=>new StaffSupplyBalanceDto(x.b.BalanceId,x.b.ItemId,x.Code,x.Name,x.b.UnitId,x.b.WarehouseId,x.b.InitialTarget,x.b.InitialIssued,x.b.AdditionalIssued,x.b.Reserved,x.b.Used,x.b.Returned,SupplyMath.Remaining(x.b),SupplyMath.Available(x.b),Convert.ToBase64String(x.b.RowVersion))).ToArray());}
}
