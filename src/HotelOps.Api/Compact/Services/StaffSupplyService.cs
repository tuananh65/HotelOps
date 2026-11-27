using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class StaffSupplyService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,DocumentService documents)
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

    // Caller holds the supply lock and outer serializable transaction. Recovery must roll back
    // the allocation AND stock issue if any item or the ensuing room reservation is unavailable.
    private async Task<StaffSupplyAllocationDto> Allocate(WorkdayAssignment assignment,WorkdayShift shift,CancellationToken ct,long? authorizingUserId=null)
    {
        var config=await db.SupplyConfigurations.AsNoTracking().Where(x=>x.BranchId==shift.BranchId&&x.IsCurrent&&x.IsActive&&x.SupplyGroup=="A"
            &&db.ItemLifecycleProfiles.Any(p=>p.BranchId==x.BranchId&&p.ItemId==x.ItemId&&p.Classification=="CONSUMABLE")).OrderBy(x=>x.ItemId).ToArrayAsync(ct);
        var reusable=await ReusableShiftConfiguration(shift.BranchId,ct);
        var allocation=new StaffSupplyAllocation{BranchId=shift.BranchId,UserId=assignment.UserId,AssignmentId=assignment.AssignmentId,CreatedAt=Now};
        db.StaffSupplyAllocations.Add(allocation);await db.SaveChangesAsync(ct);
        var balances=new List<StaffSupplyBalance>();
        var incomplete=config.FirstOrDefault(c=>c.ItemId==null||c.WarehouseId==null||c.UnitId==null
            ||c.InitialQuantity<=0||c.PerGuestQuantity<=0||!SupplyMath.ValidQuantity(c.InitialQuantity)||!SupplyMath.ValidQuantity(c.PerGuestQuantity));
        string? configurationIssue=incomplete==null?null:$"Cấu hình {incomplete.ConfigurationId} thiếu thông tin hoặc định mức không hợp lệ";
        // Do not invent required snapshot values or retain a misleading partial kit.
        foreach(var c in incomplete==null?config:[]){
            balances.Add(new StaffSupplyBalance{AllocationId=allocation.AllocationId,ConfigurationId=c.ConfigurationId,ItemId=c.ItemId!.Value,WarehouseId=c.WarehouseId!.Value,UnitId=c.UnitId!.Value,InitialTarget=c.InitialQuantity,PerGuestQuantity=c.PerGuestQuantity});
        }
        // A shift kit is indivisible: validate every item before posting any warehouse issue.
        var key=Guid.NewGuid();
        var shortage=configurationIssue!=null||(balances.Count==0&&reusable.Length==0);
        foreach(var c in reusable)if(!await ReusableShiftAvailable(c,ct))shortage=true;
        foreach(var b in balances){
            if(!await db.InventoryItems.AnyAsync(i=>i.ItemId==b.ItemId&&i.IsActive&&i.BaseUnitId==b.UnitId,ct)){
                configurationIssue??=$"Cấu hình {b.ConfigurationId}: vật tư {b.ItemId} ngừng hoạt động hoặc đơn vị không khớp đơn vị gốc";
                shortage=true;
            }
            if(!await db.Warehouses.AnyAsync(w=>w.WarehouseId==b.WarehouseId&&w.BranchId==allocation.BranchId&&w.IsActive,ct)){
                configurationIssue??=$"Cấu hình {b.ConfigurationId}: kho {b.WarehouseId} ngừng hoạt động hoặc không thuộc chi nhánh";
                shortage=true;
            }
            if(await Stock(b,ct)<b.InitialTarget)shortage=true;
        }
        // Invalid catalog/warehouse values cannot satisfy the database snapshot guard.
        // Keep the whole allocation SHORT instead of saving a partial or invented snapshot.
        if(configurationIssue!=null)balances.Clear();
        db.StaffSupplyBalances.AddRange(balances);await db.SaveChangesAsync(ct);
        allocation.Status=shortage?"SHORT":"OPEN";
        allocation.ShortageReason=shortage?"Không đủ bộ vật tư đầu ca. Chưa cấp vật tư; yêu cầu bổ sung UC-153.":null;
        if(configurationIssue!=null)allocation.ShortageReason=$"{configurationIssue}. Chưa cấp toàn bộ vật tư đầu ca; yêu cầu quản lý sửa cấu hình trước khi bổ sung UC-153.";
        if(!shortage){
            await AllocateReusable(allocation,reusable,key,ct,authorizingUserId);
            var lines=balances.Count==0?new Dictionary<long,long>():await documents.PostStaffSupply(allocation,balances.Select(b=>(b,b.InitialTarget)).ToArray(),"INITIAL",key,null,ct,authorizingUserId);
            foreach(var b in balances){b.InitialIssued=b.InitialTarget;Ledger(b,"INITIAL",b.InitialTarget,key,documentLineId:lines[b.BalanceId],authorizingUserId:authorizingUserId);}
        } else {
            var notifications=new NotificationService(db,actor,access,atomic,clock);
            notifications.Add(allocation.BranchId,allocation.UserId,"SUPPLY_INITIAL_SHORT",allocation.ShortageReason!,null,null,key);
            await notifications.ForPermission(allocation.BranchId,"CLEANING_ASSIGN","SUPPLY_INITIAL_SHORT",allocation.ShortageReason!,null,null,key,ct);
            await notifications.ForPermission(allocation.BranchId,"MR_FULFILL","SUPPLY_INITIAL_SHORT",allocation.ShortageReason!,null,null,key,ct);
        }

        if(authorizingUserId==null){
            audit.Add("StaffSupplyAllocation",allocation.AllocationId,"REUSABLE_CONFIGURATION_SNAPSHOT",allocation.BranchId,after:reusable);
            audit.Add("StaffSupplyAllocation",allocation.AllocationId,"INITIAL_KIT_ALLOCATED",allocation.BranchId);
        } else {
            db.AuditLogs.Add(new AuditLog{BranchId=allocation.BranchId,ActorUserId=null,EntityType="StaffSupplyAllocation",EntityKey=allocation.AllocationId.ToString(),Action="REUSABLE_CONFIGURATION_SNAPSHOT",AfterJson=System.Text.Json.JsonSerializer.Serialize(reusable),OccurredAt=Now});
            db.AuditLogs.Add(new AuditLog{BranchId=allocation.BranchId,ActorUserId=null,EntityType="StaffSupplyAllocation",EntityKey=allocation.AllocationId.ToString(),Action="INITIAL_KIT_ALLOCATED",OperationKey=key,AfterJson=System.Text.Json.JsonSerializer.Serialize(new{authorizingUserId,provenance="AUTO_START; persisted assignment authorization",allocation.Status,allocation.ShortageReason}),OccurredAt=Now});
        }
        await db.SaveChangesAsync(ct);return await Dto(allocation,ct);
    }

    // Called only for newly auto-started assignments, under Workday -> StaffSupplies lock order.
    internal async Task AllocateAutomatic(WorkdayAssignment assignment,CancellationToken ct)
    {
        Output.Require(db.Database.CurrentTransaction!=null,"SUPPLY_TRANSACTION_REQUIRED");
        await Lock(db,ct);
        if(await db.StaffSupplyAllocations.AnyAsync(a=>a.AssignmentId==assignment.AssignmentId,ct))return;
        Output.Require(!await db.StaffSupplyAllocations.AnyAsync(a=>a.UserId==assignment.UserId&&a.Status!="CLOSED",ct),"PREVIOUS_SUPPLIES_RETURN_PENDING");
        // Closing the allocation does not release dirty custody held for a missing-item
        // investigation. Do not mix the next shift's kit into that unresolved custody.
        Output.Require(!await HasPendingMissingCustody(assignment.UserId,ct),"PREVIOUS_MISSING_VERIFICATION_PENDING",409);
        var shift=await db.WorkdayShifts.SingleAsync(s=>s.ShiftId==assignment.ShiftId,ct);
        if(!await db.Roles.AnyAsync(r=>r.RoleId==shift.RoleId&&r.Code=="HK_STAFF",ct))return;
        await ActiveShift(assignment,shift,ct);
        await Allocate(assignment,shift,ct,assignment.CreatedBy);
    }

    internal async Task<bool> HasPendingMissingCustody(long userId,CancellationToken ct)
    {
        var heldDirty=await db.ReusablePositions.AsNoTracking().Where(p=>p.UserId==userId&&p.Status=="DIRTY"&&p.Quantity>0)
            .Select(p=>p.PositionId).ToArrayAsync(ct);
        foreach(var position in heldDirty)
            if(await ReusableMissingCustody.HasPending(db,position,ct))return true;
        return false;
    }

    // Internal request processor must validate PENDING_STOREKEEPER and approved quantities first.
    // This method rechecks role, active shift, stock and the immutable request idempotency link.
    internal Task<StaffSupplyAllocationDto> Refill(long allocationId,long requestWorkId,IReadOnlyList<SupplyIssueQuantity> requested,Guid key,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var a=await LoadAllocation(allocationId,ct);
        await access.RequireRolePermission("STOREKEEPER","MR_FULFILL",a.BranchId,ct);
        Output.Require(await db.WorkItems.AnyAsync(x=>x.WorkId==requestWorkId&&x.BranchId==a.BranchId&&x.Kind=="INTERNAL_REQUEST"&&x.CreatedBy==a.UserId,ct),"SUPPLY_REQUEST_MISMATCH");
        if(await db.AuditLogs.AnyAsync(x=>x.EntityType=="WorkItem"&&x.EntityKey==requestWorkId.ToString()&&x.Action=="REFILL_FULLY_ISSUED",ct))return await Dto(a,ct);
        await AllocationActive(a,ct);
        // A legacy partial issue cannot be issued a second time under the full-issue policy.
        Output.Require(!await db.SupplyStockDocuments.AnyAsync(d=>d.AllocationId==a.AllocationId&&d.RequestWorkId==requestWorkId&&d.Purpose=="ADDITIONAL",ct)
            &&!await db.ReusableMovements.AnyAsync(m=>m.WorkId==requestWorkId&&m.Kind=="HANDOVER",ct),"REFILL_ALREADY_ISSUED");
        Output.Require(requested.Count is >0 and <=100&&requested.Select(x=>x.ItemId).Distinct().Count()==requested.Count&&requested.All(x=>x.Quantity>0&&SupplyMath.ValidQuantity(x.Quantity)),"INVALID_SUPPLY_QUANTITIES",400);
        var balances=await db.StaffSupplyBalances.Where(x=>x.AllocationId==a.AllocationId).ToArrayAsync(ct);
        var reusable=await ReusableAllocationSnapshot(a,ct);
        var reusableIssues=new List<(SupplyConfiguration Configuration,decimal Quantity)>();
        var requestLines=await db.RequestWorkflowLines.Where(l=>l.WorkId==requestWorkId).ToArrayAsync(ct);
        Output.Require(requested.Count==requestLines.Length&&requested.All(i=>requestLines.Any(l=>l.ItemId==i.ItemId&&l.RequestedQuantity==i.Quantity)),"APPROVED_LINES_MISMATCH");
        var selected=new List<(StaffSupplyBalance Balance,decimal Quantity)>();
        foreach(var request in requested){
            var c=reusable.SingleOrDefault(x=>x.ItemId==request.ItemId);
            if(c!=null){
                Output.Require(decimal.Truncate(request.Quantity)==request.Quantity,"INVALID_SUPPLY_QUANTITY",400);
                Output.Require(!await ReusableShelfLifePolicy.Overdue(db,a.BranchId,request.ItemId,Now,ct),"REUSABLE_SHELF_REVIEW_REQUIRED");
                Output.Require(await db.InventoryItems.AnyAsync(i=>i.ItemId==request.ItemId&&i.IsActive&&i.BaseUnitId==c.UnitId,ct),"FILL_SUPPLY_CONFIGURATION_INCOMPLETE");
                Output.Require(c.WarehouseId!=null&&await db.Warehouses.AnyAsync(w=>w.WarehouseId==c.WarehouseId&&w.BranchId==a.BranchId&&w.IsActive,ct),"WAREHOUSE_UNAVAILABLE");
                var available=await db.ReusablePositions.Where(p=>p.BranchId==a.BranchId&&p.ItemId==request.ItemId&&p.WarehouseId==c.WarehouseId&&p.Status=="READY").SumAsync(p=>(decimal?)p.Quantity,ct)??0;
                Output.Require(available>=request.Quantity,"STOCK_INSUFFICIENT");
                reusableIssues.Add((c,request.Quantity));continue;
            }
            var b=balances.SingleOrDefault(x=>x.ItemId==request.ItemId)??throw new ApiException(409,"SUPPLY_ITEM_NOT_IN_SHIFT_SNAPSHOT");
            Output.Require(await db.ItemLifecycleProfiles.AnyAsync(p=>p.BranchId==a.BranchId&&p.ItemId==b.ItemId&&p.Classification=="CONSUMABLE",ct),"USE_REUSABLE_CUSTODY_WORKFLOW");
            Output.Require(await WarehouseAvailable(b,ct),"WAREHOUSE_UNAVAILABLE");Output.Require(await Stock(b,ct)>=request.Quantity,"STOCK_INSUFFICIENT");
            selected.Add((b,request.Quantity));
        }
        var accounting=new ReusableAccounting(db,clock);
        foreach(var (c,quantity) in reusableIssues){
            var source=await accounting.Position(a.BranchId,c.ItemId!.Value,"READY",c.WarehouseId,null,null,ct);
            var target=await accounting.Position(a.BranchId,c.ItemId.Value,"STAFF_HOLDING",null,a.UserId,null,ct);
            await accounting.Move(source,target,quantity,actor.Id,key,"HANDOVER","Staff refill",ct,work:requestWorkId);
        }
        var lines=selected.Count==0?new Dictionary<long,long>():await documents.PostStaffSupply(a,selected,"ADDITIONAL",key,requestWorkId,ct);
        foreach(var (b,quantity) in selected){b.AdditionalIssued+=quantity;Ledger(b,"ADDITIONAL",quantity,key,documentLineId:lines[b.BalanceId],workId:requestWorkId);}
        // An approved additional issue replenishes this shift snapshot; no manual handover is required.
        var kitCovered=balances.All(b=>b.InitialIssued+b.AdditionalIssued>=b.InitialTarget);
        foreach(var c in reusable){
            var held=await db.ReusablePositions.Where(p=>p.BranchId==a.BranchId&&p.UserId==a.UserId&&p.ItemId==c.ItemId&&p.Status=="STAFF_HOLDING")
                .SumAsync(p=>(decimal?)p.Quantity,ct)??0;
            if(held<c.InitialQuantity)kitCovered=false;
        }
        if(a.Status!="SHORT"||kitCovered)a.Status="OPEN";
        if(a.Status=="OPEN")a.ShortageReason=null;
        audit.Add("WorkItem",requestWorkId,"REFILL_FULLY_ISSUED",a.BranchId,after:new{a.AllocationId,items=requested},key:key);
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
        return null;
    }

    // Call BEFORE ending workday; recheck under the same stock lock as refill/consumption.
    internal Task<StaffSupplyAllocationDto?> End(long assignmentId,Guid key,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Key(key);await Lock(db,ct);
        var a=await db.StaffSupplyAllocations.SingleOrDefaultAsync(x=>x.AssignmentId==assignmentId,ct);
        if(a!=null){
            Output.Require(a.UserId==actor.Id,"SUPPLY_OWNER_REQUIRED",403);await access.Require("ATTENDANCE_RECORD",a.BranchId,true,ct);
            if(a.Status=="CLOSED")return await Dto(a,ct);

        }
        var blocker=await EndBlocker(assignmentId,ct);
        Output.Require(blocker==null,blocker??"SHIFT_WORK_OPEN");
        if(a==null)return null;
        Output.Require(a.UserId==actor.Id,"SUPPLY_OWNER_REQUIRED",403);await access.Require("ATTENDANCE_RECORD",a.BranchId,true,ct);
        if(a.Status=="CLOSED")return await Dto(a,ct);
        await ScheduleReturn(a,key,ct);
        return await Dto(a,ct);
    },ct);

    private async Task ActiveShift(WorkdayAssignment assignment,WorkdayShift shift,CancellationToken ct,bool allowFinishing=false)
    {
        await access.Eligible(assignment.UserId,shift.BranchId,ct);
        Output.Require(await db.Roles.AnyAsync(r=>r.RoleId==shift.RoleId&&r.Code=="HK_STAFF"&&r.IsActive,ct),"HOUSEKEEPING_SHIFT_REQUIRED");
        Output.Require(assignment.StartedAt!=null&&shift.IsActive&&
            ((assignment.Status=="WORKING"&&assignment.EndedAt==null)||(allowFinishing&&assignment.Status=="ENDED"&&assignment.EndedAt!=null)),"ACTIVE_SHIFT_REQUIRED");
        var now=Now;
        Output.Require(await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId where ur.UserId==assignment.UserId&&ur.BranchId==shift.BranchId&&ur.RevokedAt==null&&ur.AssignedAt<=now&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&role.Code=="HK_STAFF"&&role.IsActive select ur.UserRoleId).AnyAsync(ct),"HK_STAFF_REQUIRED",403);
        var zone=TimeZoneInfo.FindSystemTimeZoneById(await db.Branches.Where(x=>x.BranchId==shift.BranchId).Select(x=>x.Timezone).SingleAsync(ct));
        var start=TimeZoneInfo.ConvertTimeToUtc(assignment.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.StartMinute),zone);
        var end=TimeZoneInfo.ConvertTimeToUtc(assignment.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.EndMinute),zone);
        Output.Require(IsWithinSupplyWindow(now,start,end,allowFinishing),"ACTIVE_SHIFT_REQUIRED");
    }
    internal static bool IsWithinSupplyWindow(DateTime now,DateTime start,DateTime end,bool allowFinishing)
        =>now>=start&&(allowFinishing||now<end);
    private async Task AllocationActive(StaffSupplyAllocation a,CancellationToken ct,bool allowFinishing=false){
        Output.Require(!await db.AuditLogs.AnyAsync(x=>x.EntityType=="StaffSupplyAllocation"&&x.EntityKey==a.AllocationId.ToString()&&x.Action=="RETURN_SCHEDULED",ct),"SUPPLY_RETURN_PENDING");Output.Require(a.Status!="CLOSED","SUPPLY_ALLOCATION_CLOSED");var assignment=await db.WorkdayAssignments.SingleAsync(x=>x.AssignmentId==a.AssignmentId,ct);await ActiveShift(assignment,await db.WorkdayShifts.SingleAsync(x=>x.ShiftId==assignment.ShiftId,ct),ct,allowFinishing);}
    private async Task<StaffSupplyAllocation> LoadAllocation(long id,CancellationToken ct)=>await db.StaffSupplyAllocations.SingleOrDefaultAsync(x=>x.AllocationId==id,ct)??throw new ApiException(404,"SUPPLY_ALLOCATION_NOT_FOUND");
    private Task<bool> WarehouseAvailable(StaffSupplyBalance b,CancellationToken ct)=>db.Warehouses.AnyAsync(x=>x.WarehouseId==b.WarehouseId&&x.IsActive,ct);
    private Task<decimal> Stock(StaffSupplyBalance b,CancellationToken ct)=>StockBatchAccounting.Available(db,b.WarehouseId,b.ItemId,Now,ct);
    private void Ledger(StaffSupplyBalance b,string kind,decimal quantity,Guid key,long? workId=null,long? reservationId=null,long? documentLineId=null,long? authorizingUserId=null)=>db.StaffSupplyLedgers.Add(new StaffSupplyLedger{BalanceId=b.BalanceId,Kind=kind,Quantity=quantity,OperationKey=key,WorkId=workId,ReservationId=reservationId,DocumentLineId=documentLineId,ActorUserId=authorizingUserId??actor.Id,OccurredAt=Now});
    private async Task<StaffSupplyAllocationDto> Dto(StaffSupplyAllocation a,CancellationToken ct)
    {
        var rows=await (from b in db.StaffSupplyBalances.AsNoTracking() join item in db.InventoryItems on b.ItemId equals item.ItemId
            where b.AllocationId==a.AllocationId&&db.ItemLifecycleProfiles.Any(p=>p.BranchId==a.BranchId&&p.ItemId==b.ItemId&&p.Classification=="CONSUMABLE")
            orderby item.Name select new{b,item.Code,item.Name}).ToArrayAsync(ct);
        var lines=new List<StaffSupplyBalanceDto>();
        foreach(var x in rows)lines.Add(new(x.b.BalanceId,x.b.ItemId,x.Code,x.Name,x.b.UnitId,x.b.WarehouseId,x.b.InitialTarget,x.b.InitialIssued,x.b.AdditionalIssued,
            x.b.Reserved,x.b.Used,x.b.Returned,SupplyMath.Remaining(x.b),await StockBatchAccounting.StaffAvailable(db,x.b,Now,ct),Convert.ToBase64String(x.b.RowVersion)));
        var scheduled=await db.AuditLogs.AsNoTracking().Where(l=>l.EntityType=="StaffSupplyAllocation"&&l.EntityKey==a.AllocationId.ToString()&&l.Action=="RETURN_SCHEDULED")
            .OrderBy(l=>l.AuditId).Select(l=>(DateTime?)l.OccurredAt).FirstOrDefaultAsync(ct);
        var reusable=a.Status=="CLOSED"?Array.Empty<StaffReusableBalanceDto>():await (from p in db.ReusablePositions.AsNoTracking()
            join i in db.InventoryItems on p.ItemId equals i.ItemId
            where p.BranchId==a.BranchId&&p.UserId==a.UserId&&p.Quantity>0&&(p.Status=="STAFF_HOLDING"||p.Status=="DIRTY"||p.Status=="DAMAGED")
            orderby i.Name,p.Status select new StaffReusableBalanceDto(p.PositionId,p.ItemId,i.Code,i.Name,p.Status,p.Quantity)).ToArrayAsync(ct);
        return new(a.AllocationId,a.BranchId,a.UserId,a.AssignmentId,a.Status,a.ShortageReason,a.CreatedAt,a.ClosedAt,Convert.ToBase64String(a.RowVersion),lines,scheduled?.AddMinutes(5),reusable);
    }
}
