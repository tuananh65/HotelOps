using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class RequestWorkflowService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,StaffSupplyService supplies,DocumentService documents,NotificationService notifications)
{
    internal const string RefillType="CONSUMABLE_REFILL_REQUEST";
    internal const string ReplenishmentType="WAREHOUSE_REPLENISHMENT_REQUEST";
    private const string Supervisor="HOUSEKEEPING_SUPERVISOR";
    private static readonly string[] Finished=["FULFILLED","COMPLETED","REJECTED","CANCELLED"];
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    // Same lock order as shift supply writers. Do not take a request lock before StaffSupplies.
    private Task Lock(CancellationToken ct)=>StaffSupplyService.Lock(db,ct);

    public async Task<object> Types(long branch,CancellationToken ct)
    {
        await access.Require("IR_VIEW",branch,true,ct);
        var types=new List<object>();
        if(!await access.HasRole("OPERATION_MANAGER",branch,ct)&&await access.HasRole("HK_STAFF",branch,ct)&&await access.Allows("IR_CREATE",branch,true,ct:ct))
            types.Add(new{code=RefillType,name="Yêu cầu bổ sung tiêu hao",route="refills"});
        if(await access.HasRole("STOREKEEPER",branch,ct)&&await access.Allows("REPLENISHMENT_CREATE",branch,ct:ct))
            types.Add(new{code=ReplenishmentType,name="Yêu cầu bổ sung hàng cho kho",route="replenishments"});
        // Existing generic/room request catalogs retain their own type-specific forms and validators.
        return types;
    }

    public async Task<PageDto<RequestWorkflowSummary>> List(long branch,string queue,int page,int pageSize,string? search,CancellationToken ct)
    {
        await access.Require("IR_VIEW",branch,true,ct);
        Output.Require(queue is "mine" or "action" or "progress" or "completed" or "rejected" or "all","INVALID_REQUEST_QUEUE",400);
        var query=await Visible(branch,ct);
        var roleIds=await ActorRoles(branch,ct);
        if(queue=="action"&&await access.HasRole("OPERATION_MANAGER",branch,ct))query=query.Where(w=>db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId&&r.RequestType==ReplenishmentType));
        // Reads expose stale claims to the next shift without silently changing their owner.
        var reclaimable=queue=="action"?await ReclaimableRequests(branch,roleIds,ct):Array.Empty<long>();
        query=queue switch{
            "mine"=>query.Where(w=>w.CreatedBy==actor.Id),
            "action"=>query.Where(w=>db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId&&!Finished.Contains(r.Status)&&r.QueueRoleId!=null&&roleIds.Contains(r.QueueRoleId.Value)&&(r.HandlerUserId==null||r.HandlerUserId==actor.Id||reclaimable.Contains(r.WorkId)))
                ||!db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId)&&db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE")&&!WorkRules.Terminal.Contains(w.Status)),
            "progress"=>query.Where(w=>db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId&&!Finished.Contains(r.Status))||!db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId)&&!WorkRules.Terminal.Contains(w.Status)),
            "completed"=>query.Where(w=>db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId&&(r.Status=="FULFILLED"||r.Status=="COMPLETED"))||!db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId)&&(w.Status=="CLOSED"||w.Status=="COMPLETED")),
            "rejected"=>query.Where(w=>db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId&&r.Status=="REJECTED")||w.Status=="REJECTED"),
            _=>query};
        if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim();query=query.Where(w=>w.Title.Contains(term)||w.WorkNo.Contains(term));}
        page=Math.Max(1,page);pageSize=Math.Clamp(pageSize,1,100);
        var total=await query.CountAsync(ct);
        var rows=await query.OrderByDescending(w=>w.WorkId).Skip((page-1)*pageSize).Take(pageSize).ToArrayAsync(ct);
        var result=new List<RequestWorkflowSummary>();
        foreach(var w in rows)result.Add(await Summary(w,await db.RequestWorkflows.AsNoTracking().SingleOrDefaultAsync(r=>r.WorkId==w.WorkId,ct),ct));
        return new(result,total,page,pageSize);
    }

    private async Task<IQueryable<WorkItem>> Visible(long branch,CancellationToken ct)
    {
        var roles=await ActorRoles(branch,ct);var now=Now;
        var broad=await access.HasRole("OPERATION_MANAGER",branch,ct)||await access.Allows("IR_VIEW",null,ct:ct);
        var incidents=await access.Allows("INCIDENT_VIEW",branch,ct:ct);
        var query=db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branch&&(w.Kind=="INTERNAL_REQUEST"||incidents&&w.Kind=="INCIDENT"||db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId)));
        var legacy=InternalRequestVisibility.Apply(db.WorkItems.AsNoTracking(),db,actor.Id,now);
        return query.Where(w=>db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId)
            ? broad||w.CreatedBy==actor.Id||db.RequestWorkflows.Any(r=>r.WorkId==w.WorkId&&(r.HandlerUserId==actor.Id||r.QueueRoleId!=null&&roles.Contains(r.QueueRoleId.Value)))
                ||db.RequestWorkflowEvents.Any(e=>e.WorkId==w.WorkId&&(e.ActorUserId==actor.Id||e.NewHandlerUserId==actor.Id))
            : w.Kind=="INTERNAL_REQUEST"?legacy.Any(x=>x.WorkId==w.WorkId):incidents);
    }

    public async Task<RequestWorkflowDetail> Detail(long id,CancellationToken ct)
    {
        var work=await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w=>w.WorkId==id,ct)??throw new ApiException(404,"REQUEST_NOT_FOUND");
        await access.Require("IR_VIEW",work.BranchId,true,ct);
        Output.Require(await (await Visible(work.BranchId,ct)).AnyAsync(w=>w.WorkId==id,ct),"REQUEST_VISIBILITY_DENIED",403);
        var flow=await db.RequestWorkflows.AsNoTracking().SingleOrDefaultAsync(r=>r.WorkId==id,ct);
        if(flow==null)return new(await Summary(work,null,ct),work.Description,null,null,null,null,[],[],[]);
        var rows=await (from l in db.RequestWorkflowLines.AsNoTracking() join i in db.InventoryItems on l.ItemId equals i.ItemId where l.WorkId==id orderby i.Name select new{l,i.Name}).ToArrayAsync(ct);
        var items=new List<RequestWorkflowLineDto>();
        var maySeeStock=await access.Allows("INVENTORY_VIEW",work.BranchId,ct:ct);
        foreach(var row in rows){
            var warehouse=flow.WarehouseId??await db.StaffSupplyBalances.Where(b=>b.AllocationId==flow.AllocationId&&b.ItemId==row.l.ItemId).Select(b=>(long?)b.WarehouseId).SingleOrDefaultAsync(ct);
            decimal? stock=maySeeStock&&warehouse!=null?await Stock(warehouse.Value,row.l.ItemId,ct):null;
            var received=row.l.DocumentLineId==null?0:await Received(row.l.DocumentLineId.Value,ct);
            items.Add(new(row.l.ItemId,row.Name,row.l.UnitId,row.l.RequestedQuantity,row.l.ApprovedQuantity,row.l.StockAtRequest,row.l.MinimumAtRequest,stock,received));
        }
        var events=await db.RequestWorkflowEvents.AsNoTracking().Where(e=>e.WorkId==id).OrderBy(e=>e.RequestEventId).ToArrayAsync(ct);
        var roleNames=await db.Roles.AsNoTracking().ToDictionaryAsync(r=>r.RoleId,r=>r.Code,ct);
        var history=events.Select(e=>new RequestWorkflowHistoryDto(e.RequestEventId,e.Action,e.PreviousStatus,e.NewStatus,e.PreviousQueueRoleId is long oldRole?roleNames.GetValueOrDefault(oldRole):null,e.NewQueueRoleId is long newRole?roleNames.GetValueOrDefault(newRole):null,e.PreviousHandlerUserId,e.NewHandlerUserId,e.ActorUserId,roleNames.GetValueOrDefault(e.ActorRoleId,""),e.OccurredAt,e.Reason,e.RelatedDocumentId,e.CorrelationId,e.PreviousVersion==null?null:Convert.ToBase64String(e.PreviousVersion))).ToArray();
        var actions=await Actions(flow,work,ct);
        string? blockedReason=null;
        if(!Finished.Contains(flow.Status)&&!actions.Any(a=>a is "claim" or "release")){
            try{await HandlerAuthority(flow,ct,allowStaleClaim:true);}
            catch(ApiException e) when(e.Status is 403 or 409){blockedReason=e.Code;}
        }
        return new(await Summary(work,flow,ct),work.Description,flow.AllocationId,flow.WarehouseId,flow.DocumentId,flow.ParentRequestWorkId,items,history,actions,blockedReason);
    }

    private async Task<RequestWorkflowSummary> Summary(WorkItem w,RequestWorkflow? r,CancellationToken ct)=>new(w.WorkId,w.WorkNo,r?.RequestType??(w.Kind=="INCIDENT"?"INCIDENT":"GENERAL_INTERNAL_REQUEST"),w.Title,w.BranchId,w.RoomId,w.CreatedBy,w.RequesterDepartmentId,w.DepartmentId,r?.Status??w.Status,r?.QueueRoleId is long role?await db.Roles.Where(x=>x.RoleId==role).Select(x=>x.Code).SingleAsync(ct):null,r?.HandlerUserId,w.PriorityId,w.CreatedAt,r?.UpdatedAt??w.CompletedAt??w.CreatedAt,r?.CorrelationId,Convert.ToBase64String(r?.RowVersion??w.RowVersion),r!=null,await db.Users.AsNoTracking().Where(u=>u.UserId==w.CreatedBy).Select(u=>u.FullName).SingleAsync(ct),r!=null&&r.HandlerUserId!=null?await db.Users.AsNoTracking().Where(u=>u.UserId==r.HandlerUserId).Select(u=>u.FullName).SingleOrDefaultAsync(ct):null);

    private Task<long[]> ActorRoles(long branch,CancellationToken ct){var now=Now;return (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.RoleId where ur.UserId==actor.Id&&ur.BranchId==branch&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&role.IsActive select role.RoleId).Distinct().ToArrayAsync(ct);}
    private Task<long> Role(string code,CancellationToken ct)=>db.Roles.Where(r=>r.Code==code&&r.IsActive).Select(r=>r.RoleId).SingleAsync(ct);
    private async Task<decimal> Stock(long warehouse,long item,CancellationToken ct)=>await db.StockMovements.Where(m=>m.WarehouseId==warehouse&&m.ItemId==item).SumAsync(m=>(decimal?)m.SignedBaseQty,ct)??0;
    private async Task<decimal> Received(long line,CancellationToken ct)=>await (from m in db.StockMovements join l in db.InventoryDocumentLines on m.LineId equals l.LineId join d in db.InventoryDocuments on l.DocumentId equals d.DocumentId where l.SourceLineId==line&&d.Kind=="RECEIPT"&&!db.StockMovements.Any(r=>r.ReversalOfMovementId==m.MovementId) select (decimal?)m.SignedBaseQty).SumAsync(ct)??0;
    private static void Quantities(IReadOnlyList<SupplyIssueQuantity>? lines)=>Output.Require(lines is {Count:>0 and <=100}&&lines.Select(x=>x.ItemId).Distinct().Count()==lines.Count&&lines.All(x=>x.Quantity>0&&SupplyMath.ValidQuantity(x.Quantity)),"INVALID_REQUEST_QUANTITIES",400);

    private async Task<long?> Retry(Guid key,string action,string hash,long? id,CancellationToken ct)
    {
        Output.Key(key);
        var previous=await db.RequestWorkflowEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.ActorUserId==actor.Id&&e.OperationKey==key,ct);
        if(previous==null)return null;
        Output.Require(previous.Action==action&&previous.PayloadHash==hash&&(id==null||previous.WorkId==id),"IDEMPOTENCY_KEY_REUSED");
        return previous.WorkId;
    }

    private async Task ActiveAllocation(StaffSupplyAllocation allocation,CancellationToken ct)
    {
        var assignment=await db.WorkdayAssignments.AsNoTracking().SingleAsync(a=>a.AssignmentId==allocation.AssignmentId,ct);
        var shift=await db.WorkdayShifts.AsNoTracking().SingleAsync(s=>s.ShiftId==assignment.ShiftId,ct);
        Output.Require(allocation.Status!="CLOSED"&&assignment.UserId==allocation.UserId&&shift.BranchId==allocation.BranchId&&shift.IsActive&&assignment.Status=="WORKING"&&assignment.StartedAt!=null&&assignment.EndedAt==null,"ACTIVE_SHIFT_REQUIRED");
        var zone=TimeZoneInfo.FindSystemTimeZoneById(await db.Branches.Where(b=>b.BranchId==allocation.BranchId).Select(b=>b.Timezone).SingleAsync(ct));
        var start=TimeZoneInfo.ConvertTimeToUtc(assignment.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.StartMinute),zone);
        var end=TimeZoneInfo.ConvertTimeToUtc(assignment.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.EndMinute),zone);
        Output.Require(Now>=start&&Now<end,"ACTIVE_SHIFT_REQUIRED");await access.Eligible(allocation.UserId,allocation.BranchId,ct);
    }

    private async Task<long[]> ActiveQueueHandlers(long branch,long roleId,CancellationToken ct)
    {
        var now=Now;var zone=TimeZoneInfo.FindSystemTimeZoneById(await db.Branches.Where(b=>b.BranchId==branch&&b.IsActive).Select(b=>b.Timezone).SingleAsync(ct));
        var candidates=await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            join r in db.Roles on s.RoleId equals r.RoleId
            where r.IsActive&&s.BranchId==branch&&s.RoleId==roleId&&s.IsActive&&a.Status=="WORKING"&&a.StartedAt!=null&&a.StartedAt<=now&&a.EndedAt==null
                &&db.UserRoles.Any(ur=>ur.UserId==a.UserId&&ur.RoleId==roleId&&ur.BranchId==branch&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now))
                &&db.Users.Any(u=>u.UserId==a.UserId&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null)
                &&db.Memberships.Any(m=>m.UserId==a.UserId&&m.BranchId==branch&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
                &&db.Memberships.Any(m=>m.UserId==a.UserId&&m.BranchId==branch&&m.Kind=="EMPLOYMENT"&&m.DepartmentId==s.DepartmentId&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            select new{a.UserId,a.WorkDate,s.StartMinute,s.EndMinute}).ToArrayAsync(ct);
        var active=candidates.Where(c=>TimeZoneInfo.ConvertTimeToUtc(c.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(c.StartMinute),zone)<=now&&TimeZoneInfo.ConvertTimeToUtc(c.WorkDate.ToDateTime(TimeOnly.MinValue).AddMinutes(c.EndMinute),zone)>now).Select(c=>c.UserId).Distinct().ToArray();
        return active;
    }

    private async Task<bool> HasValidHandler(RequestWorkflow flow,CancellationToken ct)=>
        flow.HandlerUserId is long user&&flow.QueueRoleId is long role&&(await ActiveQueueHandlers(flow.BranchId,role,ct)).Contains(user);

    private async Task<long[]> ReclaimableRequests(long branch,long[] roles,CancellationToken ct)
    {
        var claims=await db.RequestWorkflows.AsNoTracking().Where(r=>r.BranchId==branch&&!Finished.Contains(r.Status)
            &&r.QueueRoleId!=null&&roles.Contains(r.QueueRoleId.Value)&&r.HandlerUserId!=null&&r.HandlerUserId!=actor.Id)
            .Select(r=>new{r.WorkId,Role=r.QueueRoleId!.Value,User=r.HandlerUserId!.Value}).ToArrayAsync(ct);
        var result=new List<long>();
        foreach(var group in claims.GroupBy(r=>r.Role)){
            var active=await ActiveQueueHandlers(branch,group.Key,ct);
            result.AddRange(group.Where(r=>!active.Contains(r.User)).Select(r=>r.WorkId));
        }
        return result.ToArray();
    }

    private async Task Route(RequestWorkflow request,string? role,CancellationToken ct)
    {
        request.QueueRoleId=role==null?null:await Role(role,ct);request.HandlerUserId=null;
        if(request.QueueRoleId is not long roleId)return;
        var active=await ActiveQueueHandlers(request.BranchId,roleId,ct);
        if(active.Length==1)request.HandlerUserId=active[0];
    }

    private async Task Record(RequestWorkflow flow,WorkItem work,string action,long role,Guid key,string hash,string? previousStatus,long? previousQueue,long? previousHandler,byte[]? version,string? reason,CancellationToken ct,long? document=null)
    {
        flow.UpdatedAt=Now;
        db.RequestWorkflowEvents.Add(new RequestWorkflowEvent{WorkId=flow.WorkId,BranchId=flow.BranchId,Action=action,PreviousStatus=previousStatus,NewStatus=flow.Status,PreviousQueueRoleId=previousQueue,NewQueueRoleId=flow.QueueRoleId,PreviousHandlerUserId=previousHandler,NewHandlerUserId=flow.HandlerUserId,ActorUserId=actor.Id,ActorRoleId=role,OccurredAt=Now,Reason=reason,RelatedDocumentId=document??flow.DocumentId,CorrelationId=flow.CorrelationId,OperationKey=key,PayloadHash=hash,PreviousVersion=version});
        audit.Add("RequestWorkflow",flow.WorkId,action,flow.BranchId,new{status=previousStatus,queue=previousQueue,handler=previousHandler},new{flow.Status,flow.QueueRoleId,flow.HandlerUserId,flow.CorrelationId},key);
        await db.SaveChangesAsync(ct);
        // Notifications are persisted in the same transaction and become visible only after commit.
        var recipients=new HashSet<long>{work.CreatedBy};
        if(flow.HandlerUserId is long handler)recipients.Add(handler);
        else if(flow.QueueRoleId is long queueRole){var now=Now;foreach(var user in await (from ur in db.UserRoles join u in db.Users on ur.UserId equals u.UserId where ur.BranchId==flow.BranchId&&ur.RoleId==queueRole&&ur.AssignedAt<=now&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>now)&&u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null&&db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==flow.BranchId&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now)) select u.UserId).Distinct().ToArrayAsync(ct))recipients.Add(user);}
        foreach(var user in await db.RequestWorkflowEvents.Where(e=>e.WorkId==flow.WorkId).Select(e=>e.ActorUserId).Distinct().ToArrayAsync(ct))recipients.Add(user);
        foreach(var user in recipients)notifications.Add(flow.BranchId,user,"REQUEST_WORKFLOW",work.Title,flow.WorkId,null,key);
        await db.SaveChangesAsync(ct);
    }
}
